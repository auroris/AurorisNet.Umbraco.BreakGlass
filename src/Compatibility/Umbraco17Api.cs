namespace BreakGlass.Compatibility;

internal sealed class Umbraco17Api(IServiceProvider services) : UmbracoApi(services)
{
    public override int MajorVersion => 17;
}
