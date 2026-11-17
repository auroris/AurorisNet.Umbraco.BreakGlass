namespace BreakGlass.Compatibility;

// The account APIs used here are shared with 17; keep an explicit adapter for each tested major.
internal sealed class Umbraco18Api(IServiceProvider services) : UmbracoApi(services)
{
    public override int MajorVersion => 18;
}
