// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(void))]

// TDS Phase 1 type forwarders - redirect System.OS types to System.Private.CoreLib
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.TypeDriverHelper))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VirtualAttribute))]

// TDS Phase 2 type forwarders
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VKernel))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VTransaction))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VContext))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VContextFlags))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VContextManager))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.VUID))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.DriverFlags))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.TypeDriverRegistry))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.TransientAttribute))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.MemorizeAttribute))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.OS.PersistentAttribute))]
