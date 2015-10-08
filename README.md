### CustomApiExplorer
#Why we need CustomApiExplorer
```c_sharp
[Route("Repositories/{id}/Process")]
[HttpGet]
public async Task<HttpResponseMessage> Process([FromUri] Id id){

}

public class Id : IComparable<Id>
{
    private readonly Guid _guid;

    #region Constructors

    public Id(string id)
    {
        this._guid = new Guid(id);
    }

    public Id(Guid guid)
    {
        this._guid = guid;
    }

    #endregion  // Constructors

    #region Public Methods

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        var target = obj as Id;
        if (target == null)
        {
            return false;
        }

        return this._guid == target._guid;
    }

    public int CompareTo(Id other)
    {
        if (other == null)
        {
            return 1;
        }

        return this._guid.CompareTo(other._guid);
    }

    public override string ToString()
    {
        return this._guid.ToString("D");
    }

    public override int GetHashCode()
    {
        return this._guid.GetHashCode();
    }

    public Guid ToGuid()
    {
        return this._guid;
    }

    #region Utility Methods

    public static bool operator ==(Id a, Id b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if ((object)a == null || (object)b == null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(Id a, Id b)
    {
        return !(a == b);
    }

    #endregion // Utility Methods

    #endregion // Public Methods
}
```
Then we can not get ApiDescription for ApiExplorer.
So we need to overwrite the ApiExplorer's source code.

#replace IApiExplorer
```c_sharp
config.Services.Replace(typeof(IApiExplorer), new CustomApiExplorer(config));
```
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
```
