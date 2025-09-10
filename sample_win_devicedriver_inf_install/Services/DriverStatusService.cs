using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using sample_win_devicedriver_inf_install.Core.Models;
using sample_win_devicedriver_inf_install.Core.Services;
using sample_win_devicedriver_inf_install.Enums;
using sample_win_devicedriver_inf_install.Models;
using sample_win_devicedriver_inf_install.Native;

namespace sample_win_devicedriver_inf_install.Services;

/// <summary>
/// ドライバ状態確認サービスの実装（コア層 - FR-014準拠）
/// デバイスマネージャー API による状態確認機能を提供します
/// </summary>
public class DriverStatusService
{
    private readonly ILogger<DriverStatusService> _logger;
    private readonly WindowsApiErrorHandler _errorHandler;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="logger">ロガー</param>
    /// <param name="errorHandler">Windows API エラーハンドラー</param>
    public DriverStatusService(
        ILogger<DriverStatusService> logger,
        WindowsApiErrorHandler errorHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    /// <summary>
    /// ドライバの状態を非同期で取得します
    /// </summary>
    /// <param name="driverPackage">確認するドライバパッケージ</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>ドライバに関連するデバイス一覧</returns>
    public async Task<List<DeviceInstance>> GetDriverStatusAsync(
        DriverPackage driverPackage,
        CancellationToken cancellationToken = default)
    {
        if (driverPackage == null)
            throw new ArgumentNullException(nameof(driverPackage));

        return await Task.Run(() =>
        {
            var devices = new List<DeviceInstance>();

            try
            {
                _logger.LogDebug("ドライバ状態確認を開始します: {DriverName}", driverPackage.Name);

                // すべてのデバイスを列挙してハードウェアIDが一致するものを検索
                using var deviceInfoSet = GetAllDevices();
                var devInfoData = SetupApi.CreateDevInfoData();
                uint memberIndex = 0;

                while (SetupApi.SetupDiEnumDeviceInfo(deviceInfoSet.Handle, memberIndex++, ref devInfoData))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var device = CreateDeviceInstance(deviceInfoSet.Handle, ref devInfoData, driverPackage);
                        if (device != null)
                        {
                            devices.Add(device);
                            _logger.LogDebug("デバイスを検出しました: {DeviceName}", device.DeviceName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "デバイス情報取得中にエラーが発生しました (Index: {MemberIndex})", memberIndex - 1);
                    }
                }

                _logger.LogInformation("ドライバ状態確認完了: {DriverName}, 検出デバイス数: {DeviceCount}",
                    driverPackage.Name, devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ドライバ状態確認中にエラーが発生しました: {DriverName}", driverPackage.Name);
                throw;
            }

            return devices;
        }, cancellationToken);
    }

    /// <summary>
    /// ハードウェアIDによるデバイス検索
    /// </summary>
    /// <param name="hardwareId">検索するハードウェアID</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>一致するデバイス一覧</returns>
    public async Task<List<DeviceInstance>> FindDevicesByHardwareIdAsync(
        string hardwareId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hardwareId))
            throw new ArgumentException("ハードウェアIDが指定されていません", nameof(hardwareId));

        return await Task.Run(() =>
        {
            var devices = new List<DeviceInstance>();

            try
            {
                using var deviceInfoSet = GetAllDevices();
                var devInfoData = SetupApi.CreateDevInfoData();
                uint memberIndex = 0;

                while (SetupApi.SetupDiEnumDeviceInfo(deviceInfoSet.Handle, memberIndex++, ref devInfoData))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var deviceHardwareIds = GetDeviceHardwareIds(deviceInfoSet.Handle, ref devInfoData);
                    if (deviceHardwareIds.Any(id => string.Equals(id, hardwareId, StringComparison.OrdinalIgnoreCase)))
                    {
                        var device = CreateDeviceInstanceFromData(deviceInfoSet.Handle, ref devInfoData);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ハードウェアID検索中にエラーが発生しました: {HardwareId}", hardwareId);
                throw;
            }

            return devices;
        }, cancellationToken);
    }

    /// <summary>
    /// インストール済みドライバの詳細情報を取得
    /// </summary>
    /// <param name="deviceInstanceId">デバイスインスタンスID</param>
    /// <param name="cancellationToken">キャンセレーショントークン</param>
    /// <returns>ドライバ詳細情報</returns>
    public async Task<DeviceInstance?> GetDriverDetailsAsync(
        string deviceInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deviceInstanceId))
            throw new ArgumentException("デバイスインスタンスIDが指定されていません", nameof(deviceInstanceId));

        return await Task.Run(() =>
        {
            try
            {
                using var deviceInfoSet = GetAllDevices();
                var devInfoData = SetupApi.CreateDevInfoData();
                uint memberIndex = 0;

                while (SetupApi.SetupDiEnumDeviceInfo(deviceInfoSet.Handle, memberIndex++, ref devInfoData))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentDeviceId = GetDeviceInstanceId(deviceInfoSet.Handle, ref devInfoData);
                    if (string.Equals(currentDeviceId, deviceInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateDeviceInstanceFromData(deviceInfoSet.Handle, ref devInfoData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デバイス詳細取得中にエラーが発生しました: {DeviceInstanceId}", deviceInstanceId);
                throw;
            }

            return null;
        }, cancellationToken);
    }

    #region Private Helper Methods

    /// <summary>
    /// すべてのデバイスの情報セットを取得
    /// </summary>
    private DeviceInfoSetHandle GetAllDevices()
    {
        var classGuid = Guid.Empty;
        var deviceInfoSet = SetupApi.SetupDiGetClassDevs(
            ref classGuid,
            null,
            IntPtr.Zero,
            SetupApi.DIGCF_ALLCLASSES | SetupApi.DIGCF_PRESENT
        );

        if (deviceInfoSet == SetupApi.INVALID_HANDLE_VALUE)
        {
            var error = _errorHandler.GetLastError("SetupDiGetClassDevs");
            throw new InvalidOperationException($"デバイス情報セットの取得に失敗しました: {error.SystemMessage}");
        }

        return new DeviceInfoSetHandle(deviceInfoSet);
    }

    /// <summary>
    /// デバイス情報からDeviceInstanceを作成
    /// </summary>
    private DeviceInstance? CreateDeviceInstance(
        IntPtr deviceInfoSet,
        ref SetupApi.SP_DEVINFO_DATA devInfoData,
        DriverPackage driverPackage)
    {
        var deviceHardwareIds = GetDeviceHardwareIds(deviceInfoSet, ref devInfoData);
        
        // ハードウェアIDが一致するかチェック
        bool isMatching = deviceHardwareIds.Any(deviceId =>
            driverPackage.HardwareIds.Any(driverId =>
                string.Equals(deviceId, driverId, StringComparison.OrdinalIgnoreCase)));

        if (!isMatching)
            return null;

        return CreateDeviceInstanceFromData(deviceInfoSet, ref devInfoData);
    }

    /// <summary>
    /// デバイス情報データからDeviceInstanceを作成
    /// </summary>
    private DeviceInstance? CreateDeviceInstanceFromData(
        IntPtr deviceInfoSet,
        ref SetupApi.SP_DEVINFO_DATA devInfoData)
    {
        try
        {
            var deviceInstanceId = GetDeviceInstanceId(deviceInfoSet, ref devInfoData);
            var deviceName = GetDeviceProperty(deviceInfoSet, ref devInfoData, SetupApi.SPDRP_FRIENDLYNAME);
            var description = GetDeviceProperty(deviceInfoSet, ref devInfoData, SetupApi.SPDRP_DEVICEDESC);
            var manufacturer = GetDeviceProperty(deviceInfoSet, ref devInfoData, SetupApi.SPDRP_MFG);
            var deviceClass = GetDeviceProperty(deviceInfoSet, ref devInfoData, SetupApi.SPDRP_CLASS);
            var hardwareIds = GetDeviceHardwareIds(deviceInfoSet, ref devInfoData);
            var status = GetDeviceStatus(deviceInfoSet, ref devInfoData);

            return DeviceInstance.CreateDetailed(
                instanceId: deviceInstanceId ?? "Unknown",
                deviceName: deviceName,
                description: description,
                manufacturer: manufacturer,
                deviceClass: deviceClass,
                hardwareIds: hardwareIds.ToList(),
                status: status
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "デバイスインスタンス作成中にエラーが発生しました");
            return null;
        }
    }

    /// <summary>
    /// デバイスのハードウェアIDを取得
    /// </summary>
    private string[] GetDeviceHardwareIds(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA devInfoData)
    {
        const uint bufferSize = 4096;
        var buffer = new byte[bufferSize];

        bool success = SetupApi.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref devInfoData,
            SetupApi.SPDRP_HARDWAREID,
            out uint propertyRegDataType,
            buffer,
            bufferSize,
            out uint requiredSize
        );

        if (!success)
        {
            return Array.Empty<string>();
        }

        // マルチ文字列（REG_MULTI_SZ）を分割
        var str = Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(requiredSize, bufferSize));
        return str.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// デバイスプロパティを取得
    /// </summary>
    private string? GetDeviceProperty(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA devInfoData, uint property)
    {
        const uint bufferSize = 1024;
        var buffer = new byte[bufferSize];

        bool success = SetupApi.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref devInfoData,
            property,
            out uint propertyRegDataType,
            buffer,
            bufferSize,
            out uint requiredSize
        );

        if (!success)
        {
            return null;
        }

        // 文字列型プロパティをデコード
        return Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(requiredSize, bufferSize)).TrimEnd('\0');
    }

    /// <summary>
    /// デバイスインスタンスIDを取得
    /// </summary>
    private string? GetDeviceInstanceId(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA devInfoData)
    {
        // デバイスインスタンスIDは特別な方法で取得する必要がある
        // ここでは簡略化してClassGuidとDevInstから生成
        return $"{devInfoData.ClassGuid}\\{devInfoData.DevInst}";
    }

    /// <summary>
    /// デバイス状態を取得
    /// </summary>
    private DeviceStatus GetDeviceStatus(IntPtr deviceInfoSet, ref SetupApi.SP_DEVINFO_DATA devInfoData)
    {
        // ConfigFlagsプロパティからデバイス状態を判定
        const uint bufferSize = sizeof(uint);
        var buffer = new byte[bufferSize];

        bool success = SetupApi.SetupDiGetDeviceRegistryProperty(
            deviceInfoSet,
            ref devInfoData,
            SetupApi.SPDRP_CONFIGFLAGS,
            out uint propertyRegDataType,
            buffer,
            bufferSize,
            out uint requiredSize
        );

        if (success && buffer.Length >= sizeof(uint))
        {
            uint configFlags = BitConverter.ToUInt32(buffer, 0);
            
            // 基本的な状態判定（簡略化）
            if ((configFlags & 0x00000001) != 0) // CONFIGFLAG_DISABLED
                return DeviceStatus.Disabled;
            if ((configFlags & 0x00000002) != 0) // CONFIGFLAG_REMOVED
                return DeviceStatus.NotPresent;
            if ((configFlags & 0x00000008) != 0) // CONFIGFLAG_FAILEDINSTALL
                return DeviceStatus.ProblemDevice;
        }

        // デフォルトは正常動作とみなす
        return DeviceStatus.Working;
    }

    #endregion

    /// <summary>
    /// デバイス情報セットのハンドルラッパー
    /// </summary>
    private class DeviceInfoSetHandle : IDisposable
    {
        public IntPtr Handle { get; }
        private bool _disposed = false;

        public DeviceInfoSetHandle(IntPtr handle)
        {
            Handle = handle;
        }

        public void Dispose()
        {
            if (!_disposed && Handle != IntPtr.Zero && Handle != SetupApi.INVALID_HANDLE_VALUE)
            {
                SetupApi.SetupDiDestroyDeviceInfoList(Handle);
                _disposed = true;
            }
        }
    }
}