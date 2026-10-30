// .NET Framework 4.8 缺少部分可空性注解类型，这里做最小补齐，
// 让同一份源码在 net48 与 net8.0-windows 下都能编译（仅编译期使用，无运行时影响）。
#if NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(
        AttributeTargets.Field
        | AttributeTargets.Parameter
        | AttributeTargets.Property
        | AttributeTargets.ReturnValue,
        Inherited = false)]
    internal sealed class AllowNullAttribute : Attribute
    {
    }
}
#endif
