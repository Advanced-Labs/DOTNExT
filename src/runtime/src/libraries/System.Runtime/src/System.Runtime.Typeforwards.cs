// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(void))]

// TDS Phase 1 type forwarders - redirect System.OS types to System.Private.CoreLib
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.TypeDriverHelper))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VirtualAttribute))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.DriverFlags))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.IFieldAccessor))]
