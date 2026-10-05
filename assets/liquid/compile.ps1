$ErrorActionPreference = 'Stop'
if (-not ('YubanShaderCompiler' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class YubanShaderCompiler {
    [ComImport,Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IBlob { [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] UIntPtr GetBufferSize(); }
    [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall)]
    static extern int D3DCompile(byte[] source,UIntPtr length,[MarshalAs(UnmanagedType.LPStr)]string name,IntPtr defines,IntPtr include,[MarshalAs(UnmanagedType.LPStr)]string entry,[MarshalAs(UnmanagedType.LPStr)]string target,uint flags,uint flags2,out IBlob code,out IBlob errors);
    public static void Compile(string sourcePath,string outputPath) {
        byte[] source=File.ReadAllBytes(sourcePath);IBlob code=null,errors=null;
        try {
            int hr=D3DCompile(source,(UIntPtr)source.Length,sourcePath,IntPtr.Zero,IntPtr.Zero,"main","ps_3_0",32768,0,out code,out errors);
            if(hr<0)throw new InvalidOperationException(errors==null?"Shader compile failed":Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
            byte[] bytes=new byte[(int)code.GetBufferSize().ToUInt64()];Marshal.Copy(code.GetBufferPointer(),bytes,0,bytes.Length);File.WriteAllBytes(outputPath,bytes);
        } finally {if(code!=null)Marshal.ReleaseComObject(code);if(errors!=null)Marshal.ReleaseComObject(errors);}
    }
}
'@
}
[YubanShaderCompiler]::Compile((Join-Path $PSScriptRoot 'LiquidSurface.hlsl'),(Join-Path $PSScriptRoot 'LiquidSurface.ps'))
