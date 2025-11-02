using UnityEngine;

/// <summary>
/// カメラ関連のユーティリティクラス
/// 角度変換や補間などの共通処理を提供
/// </summary>
public static class CameraUtility
{
    /// <summary>
    /// QuaternionのEuler角から位置計算用のYaw/Pitch角度を抽出
    /// UnityのEuler角（正=下向き）を位置計算用（正=上オフセット）に変換
    /// </summary>
    /// <param name="rotation">変換元のQuaternion</param>
    /// <param name="yaw">出力：Yaw角度（水平回転）</param>
    /// <param name="pitch">出力：Pitch角度（垂直回転、符号反転済み）</param>
    public static void ExtractYawPitchFromRotation(Quaternion rotation, out float yaw, out float pitch)
    {
        Vector3 euler = rotation.eulerAngles;
        yaw = euler.y;
        // 注: Unityの Euler.x (正=下向き) を位置計算用 (正=上オフセット) に符号反転
        pitch = -euler.x;
    }

    /// <summary>
    /// Vector3のEuler角から位置計算用のYaw/Pitch角度を抽出
    /// UnityのEuler角（正=下向き）を位置計算用（正=上オフセット）に変換
    /// </summary>
    /// <param name="eulerAngles">変換元のEuler角</param>
    /// <param name="yaw">出力：Yaw角度（水平回転）</param>
    /// <param name="pitch">出力：Pitch角度（垂直回転、符号反転済み）</param>
    public static void ExtractYawPitchFromEuler(Vector3 eulerAngles, out float yaw, out float pitch)
    {
        yaw = eulerAngles.y;
        // 注: Unityの Euler.x (正=下向き) を位置計算用 (正=上オフセット) に符号反転
        pitch = -eulerAngles.x;
    }

    /// <summary>
    /// カメラ回転をSlerpで補間
    /// </summary>
    /// <param name="from">開始回転</param>
    /// <param name="to">終了回転</param>
    /// <param name="t">補間係数 (0.0 ~ 1.0)</param>
    /// <returns>補間された回転</returns>
    public static Quaternion LerpRotation(Quaternion from, Quaternion to, float t)
    {
        return Quaternion.Slerp(from, to, t);
    }

    /// <summary>
    /// カメラ角度（Yaw/Pitch）を補間
    /// Yawは角度として、Pitchは線形補間
    /// </summary>
    /// <param name="fromYaw">開始Yaw角度</param>
    /// <param name="toYaw">終了Yaw角度</param>
    /// <param name="fromPitch">開始Pitch角度</param>
    /// <param name="toPitch">終了Pitch角度</param>
    /// <param name="t">補間係数 (0.0 ~ 1.0)</param>
    /// <param name="yaw">出力：補間されたYaw角度</param>
    /// <param name="pitch">出力：補間されたPitch角度</param>
    public static void LerpYawPitch(float fromYaw, float toYaw, float fromPitch, float toPitch, float t, out float yaw, out float pitch)
    {
        yaw = Mathf.LerpAngle(fromYaw, toYaw, t);
        pitch = Mathf.Lerp(fromPitch, toPitch, t);
    }
}
