﻿// Copyright (c) 2010-2011 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SharpDX
{
    /// <summary>
    /// Descriptor used to provide detailed message for a particular <see cref="Result"/>.
    /// </summary>
#if !WIN8
    [Serializable]
#endif
    public sealed class ResultDescriptor
    {
        private static readonly object LockDescriptor = new object();
        private static readonly List<Type> RegisteredDescriptorProvider = new List<Type>();
        private static readonly Dictionary<Result, ResultDescriptor> Descriptors = new Dictionary<Result, ResultDescriptor>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ResultDescriptor"/> class.
        /// </summary>
        /// <param name="code">The HRESULT error code.</param>
        /// <param name="module">The module (ex: SharpDX.Direct2D1).</param>
        /// <param name="apiCode">The API code (ex: D2D1_ERR_...).</param>
        /// <param name="description">The description of the result code if any.</param>
        public ResultDescriptor(Result code, string module, string apiCode, string description)
        {
            Result = code;
            Module = module;
            ApiCode = apiCode;
            Description = description;
        }

        /// <summary>
        /// Gets the result.
        /// </summary>
        public Result Result { get; private set; }

        /// <summary>
        /// Gets the module (ex: SharpDX.Direct2D1)
        /// </summary>
        public string Module { get; private set; }

        /// <summary>
        /// Gets the API code (ex: D2D1_ERR_...)
        /// </summary>
        public string ApiCode { get; private set; }

        /// <summary>
        /// Gets the description of the result code if any.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="ResultDescriptor"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="ResultDescriptor"/> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ResultDescriptor"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(ResultDescriptor other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return other.Result.Equals(this.Result);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != typeof(ResultDescriptor))
                return false;
            return Equals((ResultDescriptor)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.Result.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("HRESULT: [0x{0:X}], Module: [{1}], ApiCode: [{2}], Message: {3}", this.Result.Code, this.Module, this.ApiCode, this.Description);
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="ResultDescriptor"/> to <see cref="SharpDX.Result"/>.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator Result(ResultDescriptor result)
        {
            return result.Result;
        }
        
        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(ResultDescriptor left, Result right)
        {
            if (left == null)
                return false;
            return left.Result.Code == right.Code;
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(ResultDescriptor left, Result right)
        {
            if (left == null)
                return false;
            return left.Result.Code != right.Code;
        }

        /// <summary>
        /// Registers a <see cref="ResultDescriptor"/> provider.
        /// </summary>
        /// <param name="descriptorsProviderType">Type of the descriptors provider.</param>
        /// <remarks>
        /// Providers are usually registered at module init when SharpDX assemblies are loaded.
        /// </remarks>
        public static void RegisterProvider(Type descriptorsProviderType)
        {
            lock (LockDescriptor)
            {
                if (!RegisteredDescriptorProvider.Contains(descriptorsProviderType))
                    RegisteredDescriptorProvider.Add(descriptorsProviderType);
            }
        }

        /// <summary>
        /// Finds the specified result descriptor.
        /// </summary>
        /// <param name="result">The result code.</param>
        /// <returns>A descriptor for the specified result</returns>
        public static ResultDescriptor Find(Result result)
        {
            ResultDescriptor descriptor;
            // Check if a Win32 description exist
            var description = GetDescriptionFromResultCode(result.Code);
            if (description != null)
            {
                descriptor = new ResultDescriptor(result, "Unknown", "Unknown", description);
            }
            else
            {
                // Otherwise, check for SharpDX registered result descriptors
                lock (LockDescriptor)
                {
                    if (RegisteredDescriptorProvider.Count > 0)
                    {
                        foreach (var type in RegisteredDescriptorProvider)
                        {
                            AddDescriptorsFromType(type);
                        }
                        RegisteredDescriptorProvider.Clear();
                    }
                    if (!Descriptors.TryGetValue(result, out descriptor))
                    {
                        descriptor = new ResultDescriptor(result, "Unknown", "Unknown", "Unknown error");
                    }
                }
            }

            return descriptor;
        }

        private static void AddDescriptorsFromType(Type type)
        {
#if WIN8
            foreach(var field in type.GetTypeInfo().DeclaredFields)
            {
                if (field.FieldType == typeof(ResultDescriptor) && field.IsPublic && field.IsStatic)
                {
                    var descriptor = (ResultDescriptor)field.GetValue(null);
                    if (!Descriptors.ContainsKey(descriptor.Result))
                    {
                        Descriptors.Add(descriptor.Result, descriptor);
                    }
                }
            }
#else
            foreach(var field in type.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (field.FieldType == typeof(ResultDescriptor))
                {
                    var descriptor = (ResultDescriptor)field.GetValue(null);
                    if (!Descriptors.ContainsKey(descriptor.Result))
                    {
                        Descriptors.Add(descriptor.Result, descriptor);
                    }
                }
            }
#endif
        }

        private static string GetDescriptionFromResultCode(int resultCode)
        {
            const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
            const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
            const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

            IntPtr buffer = IntPtr.Zero;
            FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, IntPtr.Zero, resultCode, 0, ref buffer, 0, IntPtr.Zero);
            var description = Marshal.PtrToStringUni(buffer);
            Marshal.FreeHGlobal(buffer);
            return description;
        }

        [DllImport("kernel32.dll", EntryPoint = "FormatMessageW")]
        private static extern uint FormatMessageW(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId, ref IntPtr lpBuffer, int nSize, IntPtr Arguments);
    }
}