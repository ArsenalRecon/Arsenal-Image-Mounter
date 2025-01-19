//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Arsenal.ImageMounter")]
[assembly: InternalsVisibleTo("Arsenal.ImageMounter.Devio")]
[assembly: InternalsVisibleTo("Arsenal.ImageMounter.Devio.Interop")]
[assembly: InternalsVisibleTo("aim_cli")]

#pragma warning disable IDE0079 // Remove unnecessary suppression


#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata. This class should not be used by developers in source code.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

namespace System.Runtime.Versioning
{
    //
    // Summary:
    //     Base type for all platform-specific API attributes.
    internal abstract class OSPlatformAttribute : Attribute
    {
        //
        // Summary:
        //     Gets the name and optional version of the platform that the attribute applies
        //     to.
        //
        // Returns:
        //     The applicable platform name and optional version.
        public string PlatformName
        {
            get;
        }

        private protected OSPlatformAttribute(string platformName)
        {
            PlatformName = platformName;
        }
    }

    //
    // Summary:
    //     Indicates that an API is supported for a specified platform or operating system.
    //     If a version is specified, the API cannot be called from an earlier version.
    //     Multiple attributes can be applied to indicate support on multiple operating
    //     systems.
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute(string platformName) : OSPlatformAttribute(platformName)
    {
    }
}

#endif
