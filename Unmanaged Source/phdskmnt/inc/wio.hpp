/*
    Win32 overlapped I/O API functions encapsulated in C++ classes.

    Copyright (C) 2005-2007 Olof Lagerkvist.
*/

#ifndef _WIO_HPP
#define _WIO_HPP

__inline LPSTR
WideToByteAlloc(LPCWSTR lpSrc)
{
    LPSTR lpDst;
    int iReqSize =
        WideCharToMultiByte(CP_ACP, 0, lpSrc, -1, NULL, 0, NULL, NULL);
    if (iReqSize == 0)
        return NULL;

    lpDst = (LPSTR)malloc(iReqSize);
    if (lpDst == NULL)
        return NULL;

    if (WideCharToMultiByte(CP_ACP, 0, lpSrc, -1, lpDst, iReqSize, NULL, NULL)
        != iReqSize)
    {
        free(lpDst);
        return NULL;
    }

    return lpDst;
}

__inline SOCKET
ConnectTCP(sockaddr *addr, int addrlen)
{
    // Open socket
    SOCKET sd = socket(addr->sa_family, SOCK_STREAM, IPPROTO_TCP);
    if (sd == INVALID_SOCKET)
        return INVALID_SOCKET;

    if (connect(sd, addr, addrlen) == SOCKET_ERROR)
    {
        int __h_errno = WSAGetLastError();
        closesocket(sd);
        WSASetLastError(__h_errno);
        return INVALID_SOCKET;
    }

    return sd;
}

__inline SOCKET
ConnectTCP(LPCWSTR wszServer, u_short usPort)
{
    if (wszServer == NULL)
        return INVALID_SOCKET;

    if (usPort == 0)
        return INVALID_SOCKET;

#if defined(NTDDI_WIN6) && (NTDDI_VERSION >= NTDDI_WIN6)
    union
    {
        sockaddr addr;
        sockaddr_in addr4;
        sockaddr_in6 addr6;
    };

    LPCWSTR terminator;

    memset(&addr4, 0, sizeof addr4);

    NTSTATUS status = RtlIpv4StringToAddressW(wszServer, FALSE, &terminator, &addr4.sin_addr);

    if (NT_SUCCESS(status))
    {
        addr4.sin_family = AF_INET;
        addr4.sin_port = usPort;
        return ConnectTCP(&addr, sizeof addr4);
    }

    memset(&addr6, 0, sizeof addr6);

    status = RtlIpv6StringToAddressW(wszServer, &terminator, &addr6.sin6_addr);

    if (NT_SUCCESS(status))
    {
        addr6.sin6_family = AF_INET6;
        addr6.sin6_port = usPort;
        return ConnectTCP(&addr, sizeof addr6);
    }

    PADDRINFOW addrInfoPtr;
    int rc = GetAddrInfoW(wszServer, NULL, NULL, &addrInfoPtr);

    if (rc != NO_ERROR)
    {
        WSASetLastError(rc);
        return INVALID_SOCKET;
    }

    SOCKET socket = INVALID_SOCKET;

    for (PADDRINFOW addrInfo = addrInfoPtr;
        socket == INVALID_SOCKET && addrInfo != NULL;
        addrInfo = addrInfo->ai_next)
    {
        ((sockaddr_in*)addrInfo->ai_addr)->sin_port = usPort;

        socket = ConnectTCP(addrInfo->ai_addr, (int)addrInfo->ai_addrlen);
    }

    WPreserveLastError lasterror;

    FreeAddrInfoW(addrInfoPtr);

    return socket;
#else
    union
    {
        sockaddr addr;
        sockaddr_in addr4;
    };

    addr4.sin_family = AF_INET;
    addr4.sin_port = usPort;

    LPSTR szServer = WideToByteAlloc(wszServer);

    // Get server address
    addr4.sin_addr.s_addr = inet_addr(szServer);

    if (addr4.sin_addr.s_addr != INADDR_NONE)
    {
        return ConnectTCP(&addr, sizeof sockaddr_in);
    }

    // Wasn't IP? Lookup host.
    hostent* hent = gethostbyname(szServer);
    if (hent == NULL)
    {
        free(szServer);
        return INVALID_SOCKET;
    }

    free(szServer);

    for (short i = 0;
        i < hent->h_length;
        i++)
    {
        addr4.sin_addr = *(in_addr*)hent->h_addr_list[i];

        SOCKET socket = ConnectTCP(&addr, sizeof addr4);

        if (socket != INVALID_SOCKET)
        {
            return socket;
        }
    }

    return INVALID_SOCKET;
#endif
}

__inline SOCKET
ConnectTCP(LPCWSTR wszServer, LPCWSTR wszService)
{
    if (wszServer == NULL)
        return INVALID_SOCKET;

    if (wszService == NULL)
        return INVALID_SOCKET;

    u_short usPort = htons((u_short)wcstoul(wszService, NULL, 0));
    if (usPort == 0)
    {
        // Get port name for service
        LPSTR szService = WideToByteAlloc(wszService);
        servent* service = getservbyname(szService, "tcp");
        free(szService);
        if (service == NULL)
            return INVALID_SOCKET;

        usPort = service->s_port;
    }

    return ConnectTCP(wszServer, usPort);
}

/// Enhanced OVERLAPPED stucture with encapsulated API functions.
struct WOverlapped : public OVERLAPPED
{
    BOOL Read(HANDLE hFile, LPVOID lpBuf, DWORD dwLength, DWORDLONG dwStart = 0)
    {
        if (!ResetEvent())
            return FALSE;

        Offset = (DWORD)dwStart;
        OffsetHigh = (DWORD)(dwStart >> 32);
        DWORD dw;
        return ReadFile(hFile, lpBuf, dwLength, &dw, this);
    }

    BOOL Write(HANDLE hFile, LPCVOID lpBuf, DWORD dwLength,
        DWORDLONG dwStart = 0)
    {
        if (!ResetEvent())
            return FALSE;

        Offset = (DWORD)dwStart;
        OffsetHigh = (DWORD)(dwStart >> 32);
        DWORD dw;
        return WriteFile(hFile, lpBuf, dwLength, &dw, this);
    }

    DWORD BufRecv(HANDLE hFile, PVOID pBuf, DWORD dwBufSize)
    {
        DWORD dwDone = 0;
        bool bGood = true;

        for (PVOID ptr = pBuf; dwDone < dwBufSize; )
        {
            if ((!Read(hFile, ptr, dwBufSize - dwDone)) &&
                (GetLastError() != ERROR_IO_PENDING))
            {
                bGood = false;
                break;
            }

            DWORD dwReadLen;

            if (!GetResult(hFile, &dwReadLen))
            {
                bGood = false;
                break;
            }

            if (dwReadLen == 0)
                break;

            dwDone += dwReadLen;
            (*(LPBYTE*)&ptr) += dwReadLen;
        }

        if (bGood && (dwDone != dwBufSize))
        {
            SetLastError(ERROR_HANDLE_EOF);
        }

        return dwDone;
    }

    BOOL BufSend(HANDLE hFile, const void* pBuf, DWORD dwBufSize)
    {
        DWORD dwDone = 0;
        for (const void* ptr = pBuf; dwDone < dwBufSize; )
        {
            if (!Write(hFile, ptr, dwBufSize - dwDone))
                if (GetLastError() != ERROR_IO_PENDING)
                    break;

            DWORD dwWriteLen;
            if (!GetResult(hFile, &dwWriteLen))
                break;

            if (dwWriteLen == 0)
                break;

            dwDone += dwWriteLen;
            *(CONST BYTE**)& ptr += dwWriteLen;
        }

        return dwDone == dwBufSize;
    }

    BOOL ConnectNamedPipe(HANDLE hNamedPipe)
    {
        return ::ConnectNamedPipe(hNamedPipe, this);
    }

    BOOL WaitCommEvent(HANDLE hFile, LPDWORD lpEvtMask)
    {
        return ::WaitCommEvent(hFile, lpEvtMask, this);
    }

    BOOL GetResult(HANDLE hFile, LPDWORD lpNumberOfBytesTransferred,
        BOOL bWait = TRUE)
    {
        return GetOverlappedResult(hFile, this, lpNumberOfBytesTransferred, bWait);
    }

    bool Wait(DWORD dwTimeout = INFINITE)
    {
        return WaitForSingleObject(hEvent, dwTimeout) == WAIT_OBJECT_0;
    }

    bool IsComplete()
    {
        return WaitForSingleObject(hEvent, 0) == WAIT_OBJECT_0;
    }

    BOOL SetEvent()
    {
        return ::SetEvent(hEvent);
    }

    BOOL ResetEvent()
    {
        return ::ResetEvent(hEvent);
    }

    BOOL PulseEvent()
    {
        return ::PulseEvent(hEvent);
    }

    operator bool() const
    {
        return hEvent != NULL;
    }

    bool operator!() const
    {
        return hEvent == NULL;
    }

    explicit WOverlapped(OVERLAPPED& ol)
    {
        *(OVERLAPPED*)this = ol;
    }

    explicit WOverlapped(BOOL bManualReset = true, BOOL bSignalled = false)
    {
        ZeroMemory(this, sizeof * this);
        hEvent = CreateEvent(NULL, bManualReset, bSignalled, NULL);
    }

    explicit WOverlapped(LPCTSTR lpName)
    {
        ZeroMemory(this, sizeof * this);
        hEvent = OpenEvent(EVENT_ALL_ACCESS, false, lpName);
    }

    ~WOverlapped()
    {
        if (hEvent != NULL)
        {
            CloseHandle(hEvent);
        }
    }
};

#else  // __cplusplus

#endif // _WIO_HPP
