namespace Api.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipUserResolutionAttribute : Attribute;
