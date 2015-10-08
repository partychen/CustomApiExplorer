### CustomApiExplorer

#replace IApiExplorer
```c_sharp
config.Services.Replace(typeof(IApiExplorer), new CustomApiExplorer(config));

#IApiExplorer with swagger.
```c_sharp
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
