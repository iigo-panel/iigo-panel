﻿// Copyright (c) Lex Li. All rights reserved.
// 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyDescription("")]
[assembly: AssemblyCopyright("Copyright(C) Lex Li 2014-2022. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("ea1e758e-6c2e-4ed2-96eb-4427af7a76e7")]

[assembly: InternalsVisibleTo("JexusManager, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Shared, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Features.Certificates, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Features.HttpApi, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Features.HttpErrors, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Features.Logging, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("JexusManager.Features.TraceFailedRequests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("CertificateInstaller, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
[assembly: InternalsVisibleTo("Tests.JexusManager, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f7030532c52524"
+ "993841a0d09420340f3814e1b65473851bdcd18815510b035a2ae9ecee69c4cd2d9e4d6e6d5fbf"
+ "a564e86c4a4cddc9597619a31c060846ebb2e99511a0323ff82b1ebd95d6a4912502945f0e769f"
+ "190a69a439dbfb969ebad72a6f7e2e047907da4a7b9c08c6e98d5f1be8b8cafaf3eb978914059a"
+ "245d4bc1")]
