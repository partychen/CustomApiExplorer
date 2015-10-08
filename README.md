### CustomApiExplorer

#replace IApiExplorer
config.Services.Replace(typeof(IApiExplorer), new CustomApiExplorer(config));

```c_sharp
#IApiExplorer with swagger.
public class CustomApiDescriptionFilter : IApiDescriptionFilter
{
    public Collection<ApiDescription> Apply(IApiExplorer apiExplorer)
    {
        var customApiExplorer = apiExplorer as CustomApiExplorer;
        if (customApiExplorer != null)
        {
            return customApiExplorer.CustomApiDescriptions;
        }
        return apiExplorer.ApiDescriptions;
    }
}
