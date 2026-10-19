using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : PrivateThemeHiveChecks
/// desc         : 자기 ThemeProbe51 인스턴스의 구성 hive만 KEY_READ로 열어 실제 VS 테마 등록을 확인한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 전역 registry에 mount하거나 값·권한·선택 테마를 쓰지 않는다.
/// </summary>
/// ---------------------------------------------------------------------
public static class PrivateThemeHiveChecks
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RegLoadAppKey
    /// desc         : Win32의 namespace 밖 application hive를 읽기 전용 handle로 연다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 기존 파일만 허용하고 모든 handle은 검사 종료 시 닫는다.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegLoadAppKeyW")]
    private static extern int RegLoadAppKey(string path, out IntPtr handle, int access, int options, int reserved);

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Read
    /// desc         : 두 테마 이름·명시된 색상 category·AI package 부재를 전용 IDE 구성에서 읽는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 설치/구성 성공을 검사하며 실제 테마 전환 시각 검증과 구분한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static object Read(string path, string configurationRoot, int expectedCategories)
    {
        if (!File.Exists(path) || Path.GetFileName(path) != "privateregistry.bin" || !Path.GetDirectoryName(path).Contains("ThemeProbe51") || !configurationRoot.Contains("ThemeProbe51"))
            throw new InvalidOperationException("Only an existing owned ThemeProbe51 hive is allowed.");
        IntPtr pointer; int code = RegLoadAppKey(path, out pointer, 0x20019, 0, 0);
        if (code != 0) throw new Win32Exception(code, "Cannot read the owned theme configuration hive.");
        using (var handle = new SafeRegistryHandle(pointer, true))
        using (var root = RegistryKey.FromHandle(handle))
        using (var configuration = root.OpenSubKey(configurationRoot, false))
        {
            if (configuration == null) throw new InvalidOperationException("Configuration root missing: " + configurationRoot + "; top keys=" + String.Join(",", root.GetSubKeyNames()));
            var rows = new List<object>();
            string[] ids = { "{1d41c4a3-cf75-4f0a-b2ff-9a8c2d431b92}", "{8ec84919-8a34-422a-abe1-cc238ad4d4b1}" };
            string[] names = { "Grokeen Kakao Inspired", "Grokeen Dracula Inspired" };
            for (int index = 0; index < ids.Length; index++)
            using (var theme = configuration.OpenSubKey("Themes\\" + ids[index], false))
            {
                if (theme == null || Convert.ToString(theme.GetValue("Name")) != names[index]) throw new InvalidOperationException("Theme missing from actual IDE configuration: " + names[index]);
                int categories = 0; long bytes = 0;
                foreach (string name in theme.GetSubKeyNames())
                using (var category = theme.OpenSubKey(name, false))
                {
                    var data = category.GetValue("Data") as byte[];
                    if (data != null && data.Length > 16) { categories++; bytes += data.Length; }
                }
                if (categories != expectedCategories) throw new InvalidOperationException("Incomplete IDE theme data: " + names[index] + " / " + categories);
                rows.Add(new { Theme = names[index], Guid = ids[index], Categories = categories, CompiledDataBytes = bytes });
            }
            using (var ai = configuration.OpenSubKey("Packages\\{f31272f1-3188-4cca-b6e1-37c118cf7824}", false))
            {
                if (ai != null) throw new InvalidOperationException("Independent probe unexpectedly contains the AI extension registration.");
            }
            return new { Succeeded = true, Themes = rows, AiExtensionRegistered = false, ReadOnly = true, GlobalHiveMounted = false, SelectedThemeChanged = false, VisualSwitchVerified = false };
        }
    }
}
