using System.Runtime.Serialization;

namespace Qomicex.Core.AOT.Models.Enums;

/// <summary>
/// 操作系统类型枚举
/// </summary>
public enum OsType
{
    [EnumMember(Value = "windows")]
    Windows,
        
    [EnumMember(Value = "linux")]
    Linux,
        
    [EnumMember(Value = "osx")]
    Osx,
        
    [EnumMember(Value = "unknown")]
    Unknown
}