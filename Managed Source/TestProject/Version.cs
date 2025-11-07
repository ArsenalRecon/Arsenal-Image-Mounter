using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Buffers;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class Version
{
    [Fact]
    public void CheckVersion()
    {
        var ver = NativeFileVersion.GetVersion(File.ReadAllBytes(@"C:\Windows\system32\ntdll.dll"));
        Assert.Equal("Microsoft Corporation", ver.Fields["CompanyName"]);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void ValidateAuthenticodeWinPE64()
    {
        ValidateFile(@"C:\Windows\sysnative\ntdll.dll");
    }

    [Fact]
    public void ValidateAuthenticodeWinPE32()
    {
        ValidateFile(@"C:\Windows\syswow64\ntdll.dll");
    }

    private static void ValidateFile(string fileName)
    {
        var fileData = File.ReadAllBytes(fileName);

        var dirLocation = NativePE.GetRawFileDirectoryEntry(fileData, ImageDirectoryEntry.Security);

        if (dirLocation.Size == 0)
        {
            throw new InvalidOperationException("File is not signed.");
        }

        var securityDir = fileData.AsSpan(dirLocation.RelativeVirtualAddress);

        var header = MemoryMarshal.Read<NativePE.WinCertificateHeader>(securityDir);

        Assert.Equal(NativePE.CertificateType.PkcsSignedData, header.CertificateType);

        var blob = NativePE.GetCertificateBlob(securityDir);

        var signed = new SignedCms();
        signed.Decode(blob);
        signed.CheckSignature(verifySignatureOnly: true);

        var hashOk = false;

        if (NativePE.GetRawFileAuthenticodeHash(SHA256.Create, fileData, fileData.Length).AsSpan().SequenceEqual(signed.ContentInfo.Content.AsSpan()[^SHA256.HashSizeInBytes..])
            || NativePE.GetRawFileAuthenticodeHash(SHA1.Create, fileData, fileData.Length).AsSpan().SequenceEqual(signed.ContentInfo.Content.AsSpan()[^SHA1.HashSizeInBytes..])
            || NativePE.GetRawFileAuthenticodeHash(MD5.Create, fileData, fileData.Length).AsSpan().SequenceEqual(signed.ContentInfo.Content.AsSpan()[^MD5.HashSizeInBytes..]))
        {
            hashOk = true;
        }

        Assert.True(hashOk);

        var certificate = signed.SignerInfos[0].Certificate!;

        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid | X509VerificationFlags.IgnoreNotTimeNested | X509VerificationFlags.IgnoreCtlNotTimeValid;
        var result = chain.Build(certificate);

        if (!result)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, chain.ChainStatus.Select(cs => $"{cs.Status}: {cs.StatusInformation}")));
        }
    }
#endif
}
