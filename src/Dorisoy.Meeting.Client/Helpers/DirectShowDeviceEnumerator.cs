using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Dorisoy.Meeting.Client.Helpers;

/// <summary>
/// DirectShow 设备枚举器 - 用于获取摄像头等视频设备的真实名称
/// </summary>
public static class DirectShowDeviceEnumerator
{
    // DirectShow COM GUIDs
    private static readonly Guid CLSID_SystemDeviceEnum = new("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
    private static readonly Guid CLSID_VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");
    private static readonly Guid CLSID_AudioInputDeviceCategory = new("33D9A762-90C8-11D0-BD43-00A0C911CE86");

    /// <summary>
    /// 获取所有视频输入设备（摄像头）
    /// </summary>
    public static List<DeviceInfo> GetVideoInputDevices()
    {
        return GetDevices(CLSID_VideoInputDeviceCategory);
    }

    /// <summary>
    /// 获取所有音频输入设备（麦克风）
    /// </summary>
    public static List<DeviceInfo> GetAudioInputDevices()
    {
        return GetDevices(CLSID_AudioInputDeviceCategory);
    }

    /// <summary>
    /// 获取指定类别的设备列表
    /// </summary>
    private static List<DeviceInfo> GetDevices(Guid deviceCategory)
    {
        var devices = new List<DeviceInfo>();

        ICreateDevEnum? devEnum = null;
        IEnumMoniker? enumMoniker = null;
        IMoniker[]? monikers = new IMoniker[1];

        try
        {
            // 创建设备枚举器
            var type = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
            if (type == null) return devices;

            devEnum = (ICreateDevEnum?)Activator.CreateInstance(type);
            if (devEnum == null) return devices;

            // 获取指定类别的设备枚举器
            int hr = devEnum.CreateClassEnumerator(ref deviceCategory, out enumMoniker, 0);
            if (hr != 0 || enumMoniker == null) return devices;

            int deviceIndex = 0;
            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                var moniker = monikers[0];
                if (moniker == null) continue;

                try
                {
                    // 获取设备属性
                    Guid iidPropertyBag = typeof(IPropertyBag).GUID;
                    moniker.BindToStorage(null, null, ref iidPropertyBag, out object bagObj);
                    if (bagObj is IPropertyBag propertyBag)
                    {
                        try
                        {
                            // 获取 FriendlyName（设备友好名称）
                            propertyBag.Read("FriendlyName", out object nameObj, null);
                            string name = nameObj?.ToString() ?? $"Device {deviceIndex}";

                            // 尝试获取 DevicePath（设备路径，可用于唯一标识）
                            string? devicePath = null;
                            try
                            {
                                propertyBag.Read("DevicePath", out object pathObj, null);
                                devicePath = pathObj?.ToString();
                            }
                            catch
                            {
                                // DevicePath 不是所有设备都有
                            }

                            devices.Add(new DeviceInfo
                            {
                                Index = deviceIndex,
                                Name = name,
                                DevicePath = devicePath
                            });

                            deviceIndex++;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(propertyBag);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(moniker);
                }
            }
        }
        catch
        {
            // 忽略枚举异常
        }
        finally
        {
            if (enumMoniker != null)
                Marshal.ReleaseComObject(enumMoniker);
            if (devEnum != null)
                Marshal.ReleaseComObject(devEnum);
        }

        return devices;
    }

    /// <summary>
    /// 设备信息
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// 设备索引（用于 OpenCV/NAudio）
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 设备友好名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 设备路径（可选，用于唯一标识）
        /// </summary>
        public string? DevicePath { get; set; }
    }

    #region COM 接口定义

    [ComImport]
    [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator(
            [In] ref Guid pType,
            [Out] out IEnumMoniker? ppEnumMoniker,
            [In] int dwFlags);
    }

    [ComImport]
    [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
            [Out, MarshalAs(UnmanagedType.Struct)] out object pVar,
            [In] IErrorLog? pErrorLog);

        [PreserveSig]
        int Write(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
            [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
    }

    [ComImport]
    [Guid("3127CA40-446E-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IErrorLog
    {
        [PreserveSig]
        int AddError(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
            [In] IntPtr pExcepInfo);
    }

    #endregion
}
