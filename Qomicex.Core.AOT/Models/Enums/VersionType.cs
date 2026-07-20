using System.Runtime.Serialization;

namespace Qomicex.Core.AOT.Models.Enums;

/// <summary>
/// 版本类型枚举
/// </summary>
public enum VersionType
{
    [EnumMember(Value = "release")]
    Release,
        
    [EnumMember(Value = "snapshot")]
    Snapshot,
        
    [EnumMember(Value = "old_alpha")]
    OldAlpha,
        
    [EnumMember(Value = "old_beta")]
    OldBeta,
    
    [EnumMember(Value = "april_fools")]
    AprilFools,

    [EnumMember(Value = "pending")]
    Pending // 用于处理未知类型
}