// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** 
** 
** 
**
**
** Purpose: Used for binding and retrieving info about an assembly
**
**
===========================================================*/
namespace System.Reflection {
    using System;
    using System.IO;
    using System.Configuration.Assemblies;
    using System.Runtime.CompilerServices;
    using CultureInfo = System.Globalization.CultureInfo;
    using System.Runtime.Serialization;
    using System.Security.Permissions;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Diagnostics.Contracts;
    using System.Text;

    [Serializable]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_AssemblyName))]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyName : _AssemblyName, ICloneable, ISerializable, IDeserializationCallback
    {
        //
        // READ ME
        // If you modify any of these fields, you must also update the 
        // AssemblyBaseObject structure in object.h
        //
        private String          _Name;                  // Name
        private byte[]          _PublicKey;
        private byte[]          _PublicKeyToken;
        private CultureInfo     _CultureInfo;
        private String          _CodeBase;              // Potential location to get the file
        private Version         _Version;
        
        private StrongNameKeyPair            _StrongNameKeyPair;

        private SerializationInfo m_siInfo; //A temporary variable which we need during deserialization.

        private byte[]                _HashForControl;
        private AssemblyHashAlgorithm _HashAlgorithm;
        private AssemblyHashAlgorithm _HashAlgorithmForControl;

        private AssemblyVersionCompatibility _VersionCompatibility;
        private AssemblyNameFlags            _Flags;
       
        public AssemblyName()
        { 
            _HashAlgorithm = AssemblyHashAlgorithm.None;
            _VersionCompatibility = AssemblyVersionCompatibility.SameMachine;
            _Flags = AssemblyNameFlags.None;
        }
    
        // Set and get the name of the assembly. If this is a weak Name
        // then it optionally contains a site. For strong assembly names, 
        // the name partitions up the strong name's namespace
        public String Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        public Version Version
        {
            get { 
                return _Version;
            }
            set { 
                _Version = value;
            }
        }

        // Locales, internally the LCID is used for the match.
        public CultureInfo CultureInfo
        {
            get {
                return _CultureInfo;
            }
            set { 
                _CultureInfo = value; 
            }
        }

        public String CultureName
        {
            get {
                return (_CultureInfo == null) ? null : _CultureInfo.Name;
            }
            set {
                _CultureInfo = (value == null) ? null : new CultureInfo(value);
            }
        }
    
        public String CodeBase
        {
#if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
#endif
            get { return _CodeBase; }
#if FEATURE_CORECLR
            [System.Security.SecurityCritical] // auto-generated
#endif
            set { _CodeBase = value; }
        }
    
        public String EscapedCodeBase
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            {
                if (_CodeBase == null)
                    return null;
                else
                    return EscapeCodeBase(_CodeBase);
            }
        }
    
        public ProcessorArchitecture  ProcessorArchitecture
        {
            get {
                int x = (((int)_Flags) & 0x70) >> 4;
                if(x > 5) 
                    x = 0;
                return (ProcessorArchitecture)x;
            }
            set {
                int x = ((int)value) & 0x07;
                if(x <= 5) {
                    _Flags = (AssemblyNameFlags)((int)_Flags & 0xFFFFFF0F);
                    _Flags |= (AssemblyNameFlags)(x << 4);
                }
            }
        }

        [System.Runtime.InteropServices.ComVisible(false)]
        public AssemblyContentType ContentType
        {
            get
            {
                int x = (((int)_Flags) & 0x00000E00) >> 9;
                if (x > 1)
                    x = 0;
                return (AssemblyContentType)x;
            }
            set
            {
                int x = ((int)value) & 0x07;
                if (x <= 1)
                {
                    _Flags = (AssemblyNameFlags)((int)_Flags & 0xFFFFF1FF);
                    _Flags |= (AssemblyNameFlags)(x << 9);
                }
            }
        }
         
        

        // Make a copy of this assembly name.
        public Object Clone()
        {
            AssemblyName name = new AssemblyName();
            name.Init(_Name,
                      _PublicKey,
                      _PublicKeyToken,
                      _Version,
                      _CultureInfo,
                      _HashAlgorithm,
                      _VersionCompatibility,
                      _CodeBase,
                      _Flags,
                      _StrongNameKeyPair);
            name._HashForControl=_HashForControl;
            name._HashAlgorithmForControl=_HashAlgorithmForControl;
            return name;
        }

        /*
         * Get the AssemblyName for a given file. This will only work
         * if the file contains an assembly manifest. This method causes
         * the file to be opened and closed.
         */
        [System.Security.SecuritySafeCritical]  // auto-generated
        static public AssemblyName GetAssemblyName(String assemblyFile)
        {
            if(assemblyFile == null)
                throw new ArgumentNullException(nameof(assemblyFile));
            Contract.EndContractBlock();

            // Assembly.GetNameInternal() will not demand path discovery 
            //  permission, so do that first.
            string fullPath = Path.GetFullPath(assemblyFile);
            new FileIOPermission( FileIOPermissionAccess.PathDiscovery, fullPath ).Demand();
            return nGetFileInformation(fullPath);
        }
    
        internal void SetHashControl(byte[] hash, AssemblyHashAlgorithm hashAlgorithm)
        {
             _HashForControl=hash;
             _HashAlgorithmForControl=hashAlgorithm;
        }

        // The public key that is used to verify an assemblies
        // inclusion into the namespace. If the public key associated
        // with the namespace cannot verify the assembly the assembly
        // will fail to load.
        public byte[] GetPublicKey()
        {
            return _PublicKey;
        }

        public void SetPublicKey(byte[] publicKey)
        {
            _PublicKey = publicKey;

            if (publicKey == null)
                _Flags &= ~AssemblyNameFlags.PublicKey;
            else
                _Flags |= AssemblyNameFlags.PublicKey;
        }

        // The compressed version of the public key formed from a truncated hash.
        // Will throw a SecurityException if _PublicKey is invalid
        [System.Security.SecuritySafeCritical]  // auto-generated
        public byte[] GetPublicKeyToken()
        {
            if (_PublicKeyToken == null)
                _PublicKeyToken = nGetPublicKeyToken();
            return _PublicKeyToken;
        }

        public void SetPublicKeyToken(byte[] publicKeyToken)
        {
            _PublicKeyToken = publicKeyToken;
        }

        // Flags modifying the name. So far the only flag is PublicKey, which
        // indicates that a full public key and not the compressed version is
        // present. 
        // Processor Architecture flags are set only through ProcessorArchitecture
        // property and can't be set or retrieved directly
        // Content Type flags are set only through ContentType property and can't be 
        // set or retrieved directly
        public AssemblyNameFlags Flags
        {
            get { return (AssemblyNameFlags)((uint)_Flags & 0xFFFFF10F); }
            set {
                _Flags &= unchecked((AssemblyNameFlags)0x00000EF0);
                _Flags |= (value & unchecked((AssemblyNameFlags)0xFFFFF10F));
            }
        }

        public AssemblyHashAlgorithm HashAlgorithm
        {
            get { return _HashAlgorithm; }
            set { _HashAlgorithm = value; }
        }
        
        public AssemblyVersionCompatibility VersionCompatibility
        {
            get { return _VersionCompatibility; }
            set { _VersionCompatibility = value; }
        }

        public StrongNameKeyPair KeyPair
        {
            get { return _StrongNameKeyPair; }
            set { _StrongNameKeyPair = value; }
        }
       
        public String FullName
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                return nToString();
            }
        }
    
        // Returns the stringized version of the assembly name.
        public override String ToString()
        {
            String s = FullName;
            if(s == null) 
                return base.ToString();
            else 
                return s;
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            Contract.EndContractBlock();

            //Allocate the serialization info and serialize our static data.
            info.AddValue("_Name", _Name);
            info.AddValue("_PublicKey", _PublicKey, typeof(byte[]));
            info.AddValue("_PublicKeyToken", _PublicKeyToken, typeof(byte[]));
#if FEATURE_USE_LCID
            info.AddValue("_CultureInfo", (_CultureInfo == null) ? -1 :_CultureInfo.LCID);
#endif
            info.AddValue("_CodeBase", _CodeBase);
            info.AddValue("_Version", _Version);
            info.AddValue("_HashAlgorithm", _HashAlgorithm, typeof(AssemblyHashAlgorithm));
            info.AddValue("_HashAlgorithmForControl", _HashAlgorithmForControl, typeof(AssemblyHashAlgorithm));
            info.AddValue("_StrongNameKeyPair", _StrongNameKeyPair, typeof(StrongNameKeyPair));
            info.AddValue("_VersionCompatibility", _VersionCompatibility, typeof(AssemblyVersionCompatibility));
            info.AddValue("_Flags", _Flags, typeof(AssemblyNameFlags));
            info.AddValue("_HashForControl",_HashForControl,typeof(byte[]));
       }

        public void OnDeserialization(Object sender)
        {
            // Deserialization has already been performed
            if (m_siInfo == null)
                return;

            _Name = m_siInfo.GetString("_Name");
            _PublicKey = (byte[]) m_siInfo.GetValue("_PublicKey", typeof(byte[]));
            _PublicKeyToken = (byte[]) m_siInfo.GetValue("_PublicKeyToken", typeof(byte[]));
#if FEATURE_USE_LCID
            int lcid = (int)m_siInfo.GetInt32("_CultureInfo");
            if (lcid != -1)
                _CultureInfo = new CultureInfo(lcid);
#endif

            _CodeBase = m_siInfo.GetString("_CodeBase");
            _Version = (Version) m_siInfo.GetValue("_Version", typeof(Version));
            _HashAlgorithm = (AssemblyHashAlgorithm) m_siInfo.GetValue("_HashAlgorithm", typeof(AssemblyHashAlgorithm));
            _StrongNameKeyPair = (StrongNameKeyPair) m_siInfo.GetValue("_StrongNameKeyPair", typeof(StrongNameKeyPair));
            _VersionCompatibility = (AssemblyVersionCompatibility)m_siInfo.GetValue("_VersionCompatibility", typeof(AssemblyVersionCompatibility));
            _Flags = (AssemblyNameFlags) m_siInfo.GetValue("_Flags", typeof(AssemblyNameFlags));

            try {
                _HashAlgorithmForControl = (AssemblyHashAlgorithm) m_siInfo.GetValue("_HashAlgorithmForControl", typeof(AssemblyHashAlgorithm));
                _HashForControl = (byte[]) m_siInfo.GetValue("_HashForControl", typeof(byte[]));    
            }
            catch (SerializationException) { // RTM did not have these defined
                _HashAlgorithmForControl = AssemblyHashAlgorithm.None;
                _HashForControl = null;
            }

            m_siInfo = null;
        }

        // Constructs a new AssemblyName during deserialization.
        internal AssemblyName(SerializationInfo info, StreamingContext context)
        {
            //The graph is not valid until OnDeserialization() has been called.
            m_siInfo = info; 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public AssemblyName(String assemblyName)
        {
            if (assemblyName == null)
                throw new ArgumentNullException(nameof(assemblyName));
            Contract.EndContractBlock();
            if ((assemblyName.Length == 0) ||
                (assemblyName[0] == '\0'))
                throw new ArgumentException(Environment.GetResourceString("Format_StringZeroLength"));

            _Name = assemblyName;
            nInit();
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        static public bool ReferenceMatchesDefinition(AssemblyName reference,
                                                             AssemblyName definition)
        {
            // Optimization for common use case
            if (Object.ReferenceEquals(reference, definition))
            {
                return true;
            }
            return ReferenceMatchesDefinitionInternal(reference, definition, true);
        }

        
        /// "parse" tells us to parse the simple name of the assembly as if it was the full name
        /// almost never the right thing to do, but needed for compat
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern bool ReferenceMatchesDefinitionInternal(AssemblyName reference,
                                                                     AssemblyName definition,
                                                                     bool parse);  



        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal extern void nInit(out RuntimeAssembly assembly, bool forIntrospection, bool raiseResolveEvent);

        [System.Security.SecurityCritical]  // auto-generated
        internal void nInit()
        {
            RuntimeAssembly dummy = null;
            nInit(out dummy, false, false);
        }

        internal void SetProcArchIndex(PortableExecutableKinds pek, ImageFileMachine ifm)
        {
            ProcessorArchitecture = CalculateProcArchIndex(pek, ifm, _Flags);
        }

        internal static ProcessorArchitecture CalculateProcArchIndex(PortableExecutableKinds pek, ImageFileMachine ifm, AssemblyNameFlags flags)
        {
            if (((uint)flags & 0xF0) == 0x70)
                return ProcessorArchitecture.None;

            if ((pek & System.Reflection.PortableExecutableKinds.PE32Plus) == System.Reflection.PortableExecutableKinds.PE32Plus)
            {
                switch (ifm)
                {
                    case System.Reflection.ImageFileMachine.IA64:
                        return ProcessorArchitecture.IA64;
                    case System.Reflection.ImageFileMachine.AMD64:
                        return ProcessorArchitecture.Amd64;
                    case System.Reflection.ImageFileMachine.I386:
                        if ((pek & System.Reflection.PortableExecutableKinds.ILOnly) == System.Reflection.PortableExecutableKinds.ILOnly)
                            return ProcessorArchitecture.MSIL;
                        break;
                }
            }
            else
            {
                if (ifm == System.Reflection.ImageFileMachine.I386)
                {
                    if ((pek & System.Reflection.PortableExecutableKinds.Required32Bit) == System.Reflection.PortableExecutableKinds.Required32Bit)
                        return ProcessorArchitecture.X86;

                    if ((pek & System.Reflection.PortableExecutableKinds.ILOnly) == System.Reflection.PortableExecutableKinds.ILOnly)
                        return ProcessorArchitecture.MSIL;

                    return ProcessorArchitecture.X86;
                }
                if (ifm == System.Reflection.ImageFileMachine.ARM)
                {
                    return ProcessorArchitecture.Arm;
                }
            }
            return ProcessorArchitecture.None;
        }

        internal void Init(String name, 
                           byte[] publicKey,
                           byte[] publicKeyToken,
                           Version version,
                           CultureInfo cultureInfo,
                           AssemblyHashAlgorithm hashAlgorithm,
                           AssemblyVersionCompatibility versionCompatibility,
                           String codeBase,
                           AssemblyNameFlags flags,
                           StrongNameKeyPair keyPair) // Null if ref, matching Assembly if def
        {
            _Name = name;

            if (publicKey != null) {
                _PublicKey = new byte[publicKey.Length];
                Array.Copy(publicKey, _PublicKey, publicKey.Length);
            }
    
            if (publicKeyToken != null) {
                _PublicKeyToken = new byte[publicKeyToken.Length];
                Array.Copy(publicKeyToken, _PublicKeyToken, publicKeyToken.Length);
            }
    
            if (version != null)
                _Version = (Version) version.Clone();

            _CultureInfo = cultureInfo;
            _HashAlgorithm = hashAlgorithm;
            _VersionCompatibility = versionCompatibility;
            _CodeBase = codeBase;
            _Flags = flags;
            _StrongNameKeyPair = keyPair;
        }

        // This call opens and closes the file, but does not add the
        // assembly to the domain.
        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern AssemblyName nGetFileInformation(String s);

        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern String nToString();

        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private extern byte[] nGetPublicKeyToken();

        [System.Security.SecurityCritical]  // auto-generated
        static internal String EscapeCodeBase(String codebase)
        {
            if (codebase == null)
                return string.Empty;
                
            int position = 0;
            char[] dest = EscapeString(codebase, 0, codebase.Length, null, ref position, true, c_DummyChar, c_DummyChar, c_DummyChar);
            if (dest == null)
                return codebase;

            return new string(dest, 0, position);
        }

        // This implementation of EscapeString has been copied from System.Private.Uri from corefx repo
        // - forceX characters are always escaped if found
        // - rsvd character will remain unescaped
        //
        // start    - starting offset from input
        // end      - the exclusive ending offset in input
        // destPos  - starting offset in dest for output, on return this will be an exclusive "end" in the output.
        //
        // In case "dest" has lack of space it will be reallocated by preserving the _whole_ content up to current destPos
        //
        // Returns null if nothing has to be escaped AND passed dest was null, otherwise the resulting array with the updated destPos
        //
        internal unsafe static char[] EscapeString(string input, int start, int end, char[] dest, ref int destPos,
            bool isUriString, char force1, char force2, char rsvd)
        {
            int i = start;
            int prevInputPos = start;
            byte* bytes = stackalloc byte[c_MaxUnicodeCharsReallocate * c_MaxUTF_8BytesPerUnicodeChar];   // 40*4=160

            fixed (char* pStr = input)
            {
                for (; i < end; ++i)
                {
                    char ch = pStr[i];

                    // a Unicode ?
                    if (ch > '\x7F')
                    {
                        short maxSize = (short)Math.Min(end - i, (int)c_MaxUnicodeCharsReallocate - 1);

                        short count = 1;
                        for (; count < maxSize && pStr[i + count] > '\x7f'; ++count)
                            ;

                        // Is the last a high surrogate?
                        if (pStr[i + count - 1] >= 0xD800 && pStr[i + count - 1] <= 0xDBFF)
                        {
                            // Should be a rare case where the app tries to feed an invalid Unicode surrogates pair
                            if (count == 1 || count == end - i)
                                throw new FormatException(Environment.GetResourceString("Arg_FormatException"));
                            // need to grab one more char as a Surrogate except when it's a bogus input
                            ++count;
                        }

                        dest = EnsureDestinationSize(pStr, dest, i,
                            (short)(count * c_MaxUTF_8BytesPerUnicodeChar * c_EncodedCharsPerByte),
                            c_MaxUnicodeCharsReallocate * c_MaxUTF_8BytesPerUnicodeChar * c_EncodedCharsPerByte,
                            ref destPos, prevInputPos);

                        short numberOfBytes = (short)Encoding.UTF8.GetBytes(pStr + i, count, bytes,
                            c_MaxUnicodeCharsReallocate * c_MaxUTF_8BytesPerUnicodeChar);

                        // This is the only exception that built in UriParser can throw after a Uri ctor.
                        // Should not happen unless the app tries to feed an invalid Unicode String
                        if (numberOfBytes == 0)
                            throw new FormatException(Environment.GetResourceString("Arg_FormatException"));

                        i += (count - 1);

                        for (count = 0; count < numberOfBytes; ++count)
                            EscapeAsciiChar((char)bytes[count], dest, ref destPos);

                        prevInputPos = i + 1;
                    }
                    else if (ch == '%' && rsvd == '%')
                    {
                        // Means we don't reEncode '%' but check for the possible escaped sequence
                        dest = EnsureDestinationSize(pStr, dest, i, c_EncodedCharsPerByte,
                            c_MaxAsciiCharsReallocate * c_EncodedCharsPerByte, ref destPos, prevInputPos);
                        if (i + 2 < end && EscapedAscii(pStr[i + 1], pStr[i + 2]) != c_DummyChar)
                        {
                            // leave it escaped
                            dest[destPos++] = '%';
                            dest[destPos++] = pStr[i + 1];
                            dest[destPos++] = pStr[i + 2];
                            i += 2;
                        }
                        else
                        {
                            EscapeAsciiChar('%', dest, ref destPos);
                        }
                        prevInputPos = i + 1;
                    }
                    else if (ch == force1 || ch == force2)
                    {
                        dest = EnsureDestinationSize(pStr, dest, i, c_EncodedCharsPerByte,
                            c_MaxAsciiCharsReallocate * c_EncodedCharsPerByte, ref destPos, prevInputPos);
                        EscapeAsciiChar(ch, dest, ref destPos);
                        prevInputPos = i + 1;
                    }
                    else if (ch != rsvd && (isUriString ? !IsReservedUnreservedOrHash(ch) : !IsUnreserved(ch)))
                    {
                        dest = EnsureDestinationSize(pStr, dest, i, c_EncodedCharsPerByte,
                            c_MaxAsciiCharsReallocate * c_EncodedCharsPerByte, ref destPos, prevInputPos);
                        EscapeAsciiChar(ch, dest, ref destPos);
                        prevInputPos = i + 1;
                    }
                }

                if (prevInputPos != i)
                {
                    // need to fill up the dest array ?
                    if (prevInputPos != start || dest != null)
                        dest = EnsureDestinationSize(pStr, dest, i, 0, 0, ref destPos, prevInputPos);
                }
            }

            return dest;
        }

        //
        // ensure destination array has enough space and contains all the needed input stuff
        //
        private unsafe static char[] EnsureDestinationSize(char* pStr, char[] dest, int currentInputPos,
            short charsToAdd, short minReallocateChars, ref int destPos, int prevInputPos)
        {
            if ((object)dest == null || dest.Length < destPos + (currentInputPos - prevInputPos) + charsToAdd)
            {
                // allocating or reallocating array by ensuring enough space based on maxCharsToAdd.
                char[] newresult = new char[destPos + (currentInputPos - prevInputPos) + minReallocateChars];

                if ((object)dest != null && destPos != 0)
                    Buffer.BlockCopy(dest, 0, newresult, 0, destPos << 1);
                dest = newresult;
            }

            // ensuring we copied everything form the input string left before last escaping
            while (prevInputPos != currentInputPos)
                dest[destPos++] = pStr[prevInputPos++];
            return dest;
        }
        
        internal static void EscapeAsciiChar(char ch, char[] to, ref int pos)
        {
            to[pos++] = '%';
            to[pos++] = s_hexUpperChars[(ch & 0xf0) >> 4];
            to[pos++] = s_hexUpperChars[ch & 0xf];
        }

        internal static char EscapedAscii(char digit, char next)
        {
            if (!(((digit >= '0') && (digit <= '9'))
                || ((digit >= 'A') && (digit <= 'F'))
                || ((digit >= 'a') && (digit <= 'f'))))
            {
                return c_DummyChar;
            }

            int res = (digit <= '9')
                ? ((int)digit - (int)'0')
                : (((digit <= 'F')
                ? ((int)digit - (int)'A')
                : ((int)digit - (int)'a'))
                   + 10);

            if (!(((next >= '0') && (next <= '9'))
                || ((next >= 'A') && (next <= 'F'))
                || ((next >= 'a') && (next <= 'f'))))
            {
                return c_DummyChar;
            }

            return (char)((res << 4) + ((next <= '9')
                    ? ((int)next - (int)'0')
                    : (((next <= 'F')
                        ? ((int)next - (int)'A')
                        : ((int)next - (int)'a'))
                       + 10)));
        }       

        private static unsafe bool IsReservedUnreservedOrHash(char c)
        {
            if (IsUnreserved(c))
            {
                return true;
            }
            return (RFC3986ReservedMarks.IndexOf(c) >= 0);
        }

        internal static unsafe bool IsUnreserved(char c)
        {
            if (IsAsciiLetterOrDigit(c))
            {
                return true;
            }
            return (RFC3986UnreservedMarks.IndexOf(c) >= 0);
        }

        //Only consider ASCII characters
        internal static bool IsAsciiLetter(char character)
        {
            return (character >= 'a' && character <= 'z') ||
                   (character >= 'A' && character <= 'Z');
        }

        internal static bool IsAsciiLetterOrDigit(char character)
        {
            return IsAsciiLetter(character) || (character >= '0' && character <= '9');
        }
        
        private static readonly char[] s_hexUpperChars = {
                                   '0', '1', '2', '3', '4', '5', '6', '7',
                                   '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
        internal const char c_DummyChar = (char)0xFFFF;     //An Invalid Unicode character used as a dummy char passed into the parameter                                   
        private const short c_MaxAsciiCharsReallocate = 40;
        private const short c_MaxUnicodeCharsReallocate = 40;
        private const short c_MaxUTF_8BytesPerUnicodeChar = 4;
        private const short c_EncodedCharsPerByte = 3;     
        private const string RFC3986ReservedMarks = @":/?#[]@!$&'()*+,;=";
        private const string RFC3986UnreservedMarks = @"-._~";
    }
}
