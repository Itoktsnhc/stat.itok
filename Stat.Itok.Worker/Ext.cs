namespace Stat.Itok.Worker;

public static class Constant
{
    public const string ConfigFileEnvKey = "STAT_ITOK_CONFIG";
    public const string DefaultConfigName = "appsettings.json";
}

public static class Helper
{
    public static string GetConfigFileName()
    {
        var env = Environment.GetEnvironmentVariable(Constant.ConfigFileEnvKey);
        return string.IsNullOrEmpty(env) ? Constant.DefaultConfigName : env;
    }
    public static IServiceCollection AddConfigByType<TConfig>(this IServiceCollection svc, IConfiguration config)
        where TConfig : class
    {
        return svc.Configure<TConfig>(config.GetSection(typeof(TConfig).Name));
    }
}