param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [Parameter(Mandatory = $true)][string]$IconPath
)

$ErrorActionPreference = 'Stop'
$executable = [System.IO.Path]::GetFullPath($ExecutablePath)
$icon = [System.IO.File]::ReadAllBytes([System.IO.Path]::GetFullPath($IconPath))
if ($icon.Length -lt 6 -or [BitConverter]::ToUInt16($icon, 0) -ne 0 -or [BitConverter]::ToUInt16($icon, 2) -ne 1) {
    throw "Invalid ICO file: $IconPath"
}

$count = [BitConverter]::ToUInt16($icon, 4)
if ($count -lt 1) { throw "ICO file contains no images: $IconPath" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MqCtrlResourceWriter
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr BeginUpdateResource(string fileName, bool deleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndUpdateResource(IntPtr update, bool discard);
}
'@

$update = [MqCtrlResourceWriter]::BeginUpdateResource($executable, $false)
if ($update -eq [IntPtr]::Zero) { throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()) }

$committed = $false
try {
    $group = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($group)
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write($count)

    for ($index = 0; $index -lt $count; $index++) {
        $entryOffset = 6 + 16 * $index
        $imageLength = [BitConverter]::ToUInt32($icon, $entryOffset + 8)
        $imageOffset = [BitConverter]::ToUInt32($icon, $entryOffset + 12)
        if ($imageOffset + $imageLength -gt $icon.Length) { throw "ICO image $index exceeds file bounds." }

        $resourceId = $index + 1
        $image = [byte[]]::new($imageLength)
        [Array]::Copy($icon, $imageOffset, $image, 0, $imageLength)
        if (-not [MqCtrlResourceWriter]::UpdateResource($update, [IntPtr]3, [IntPtr]$resourceId, 0, $image, $imageLength)) {
            throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }

        $writer.Write($icon, $entryOffset, 12)
        $writer.Write([UInt16]$resourceId)
    }

    $groupData = $group.ToArray()
    if (-not [MqCtrlResourceWriter]::UpdateResource($update, [IntPtr]14, [IntPtr]1, 0, $groupData, [UInt32]$groupData.Length)) {
        throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
    }

    if (-not [MqCtrlResourceWriter]::EndUpdateResource($update, $false)) {
        $update = [IntPtr]::Zero
        throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
    }
    $update = [IntPtr]::Zero
    $committed = $true
}
finally {
    $writer.Dispose()
    $group.Dispose()
    if ($update -ne [IntPtr]::Zero) { [void][MqCtrlResourceWriter]::EndUpdateResource($update, $true) }
}

if (-not $committed) { throw "Failed to embed icon into $executable" }
Write-Host "Embedded $count icon sizes into $executable"
