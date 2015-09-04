#ifndef _WSCM_H_
#define _WSCM_H_

class WSCObject
{
    SC_HANDLE handle;

public:
    SC_HANDLE Handle()
    {
        return handle;
    }

    operator bool()
    {
        return handle != NULL;
    }

    bool operator !()
    {
        return handle == NULL;
    }

    void Close()
    {
        if (handle != NULL)
        {
            CloseServiceHandle(handle);
            handle = NULL;
        }
    }

    void Swap(WSCObject &Other)
    {
        auto h = handle;
        handle = Other.handle;
        Other.handle = h;
    }

    WSCObject(SC_HANDLE ExistingHandle)
        : handle(ExistingHandle) {}

    ~WSCObject()
    {
        Close();
    }
};

class WSCManager : public WSCObject
{
public:
    WSCManager(LPCTSTR ComputerName = NULL,
        LPCTSTR DatabaseName = NULL,
        DWORD Access = SC_MANAGER_ALL_ACCESS)
        : WSCObject(OpenSCManager(ComputerName,
        DatabaseName, Access)) {}
};

class WSCService : public WSCObject
{
public:
    /// Open an existing service
    WSCService(SC_HANDLE ServiceHandle,
        LPCTSTR ServiceName,
        DWORD Access = SC_MANAGER_ALL_ACCESS)
        : WSCObject(OpenService(ServiceHandle,
        ServiceName, Access)) {}

    /// Create a new existing service
    WSCService(
        IN            SC_HANDLE hSCManager,
        IN            LPCTSTR lpServiceName,
        IN OPTIONAL   LPCTSTR lpDisplayName,
        IN            DWORD dwDesiredAccess,
        IN            DWORD dwServiceType,
        IN            DWORD dwStartType,
        IN            DWORD dwErrorControl,
        IN OPTIONAL   LPCTSTR lpBinaryPathName = NULL,
        IN OPTIONAL   LPCTSTR lpLoadOrderGroup = NULL,
        OUT OPTIONAL  LPDWORD lpdwTagId = NULL,
        IN OPTIONAL   LPCTSTR lpDependencies = NULL,
        IN OPTIONAL   LPCTSTR lpServiceStartName = NULL,
        IN OPTIONAL   LPCTSTR lpPassword = NULL
        )
        : WSCObject(CreateService(
        hSCManager, lpServiceName, lpDisplayName,
        dwDesiredAccess, dwServiceType, dwStartType,
        dwErrorControl, lpBinaryPathName, lpLoadOrderGroup,
        lpdwTagId, lpDependencies, lpServiceStartName, lpPassword)) {}
};

#endif // _WSCM_H_