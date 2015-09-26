using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Description;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;

namespace Microsoft.CustomApiExplorer
{
    public class CustomApiExplorer : ApiExplorer
    {
        private readonly Lazy<Collection<ApiDescription>> _customApiDescriptions;
        private readonly HttpConfiguration _customconfig;
        private static readonly Regex _actionVariableRegex = new Regex(String.Format(CultureInfo.CurrentCulture, "{{{0}}}", RouteValueKeys.Action), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _controllerVariableRegex = new Regex(String.Format(CultureInfo.CurrentCulture, "{{{0}}}", RouteValueKeys.Controller), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public CustomApiExplorer(HttpConfiguration configuration)
            :base(configuration)
        {
            _customconfig = configuration;
            _customApiDescriptions = new Lazy<Collection<ApiDescription>>(InitializeCustomApiDescriptions);
        }

        public Collection<ApiDescription> CustomApiDescriptions
        {
            get
            {
                return _customApiDescriptions.Value;
            }
        }

        private Collection<ApiDescription> InitializeCustomApiDescriptions()
        {
            var apiDescriptions = new Collection<ApiDescription>();
            IHttpControllerSelector controllerSelector = _customconfig.Services.GetHttpControllerSelector();
            IDictionary<string, HttpControllerDescriptor> controllerMappings = controllerSelector.GetControllerMapping();
            if (controllerMappings != null)
            {
                var descriptionComparer = new ApiDescriptionComparer();
                foreach (IHttpRoute route in (IEnumerable<IHttpRoute>) this.CallBasePrivateMethod("FlattenRoutes", new object[] { _customconfig.Routes }))
                {
                    var directRouteCandidates = route.CallExtensionMethod("GetDirectRouteCandidates",null, typeof(IHttpRoute));

                    var directRouteController = this.CallBasePrivateMethod("GetDirectRouteController", new object[] { directRouteCandidates });

                    var descriptionsFromRoute = (directRouteController != null && directRouteCandidates != null)
                        ? ExploreDirectRoute((HttpControllerDescriptor)directRouteController, (IEnumerable<object>)directRouteCandidates, route)
                        : ExploreRouteControllers(controllerMappings, route);

                    // Remove ApiDescription that will lead to ambiguous action matching.
                    // E.g. a controller with Post() and PostComment(). When the route template is {controller}, it produces POST /controller and POST /controller.
                    //descriptionsFromRoute = RemoveInvalidApiDescriptions(descriptionsFromRoute);
                    descriptionsFromRoute = (Collection<ApiDescription>)this.CallBasePrivateMethod("RemoveInvalidApiDescriptions",
                        new object[] {descriptionsFromRoute});

                    foreach (ApiDescription description in descriptionsFromRoute)
                    {
                        // Do not add the description if the previous route has a matching description with the same HTTP method and relative path.
                        // E.g. having two routes with the templates "api/Values/{id}" and "api/{controller}/{id}" can potentially produce the same
                        // relative path "api/Values/{id}" but only the first one matters.
                        if (!apiDescriptions.Contains(description, descriptionComparer))
                        {
                            apiDescriptions.Add(description);
                        }
                    }
                    
                }
            }

            return apiDescriptions;
        }
   
        private Collection<ApiDescription> ExploreDirectRoute(HttpControllerDescriptor controllerDescriptor, IEnumerable<object> candidates, IHttpRoute route)
        {
            var descriptions = new Collection<ApiDescription>();

            if (ShouldExploreController(controllerDescriptor.ControllerName, controllerDescriptor, route))
            {
                foreach (var action in candidates)
                {
                    var actionDescriptor = (HttpActionDescriptor)action.CallPrivateMethod("get_ActionDescriptor");
                    string actionName = actionDescriptor.ActionName;

                    if (ShouldExploreAction(actionName, actionDescriptor, route))
                    {
                        string routeTemplate = route.RouteTemplate;
                        if (_actionVariableRegex.IsMatch(routeTemplate))
                        {
                            // expand {action} variable
                            routeTemplate = _actionVariableRegex.Replace(routeTemplate, actionName);
                        }

                        PopulateActionDescriptions(actionDescriptor, route, routeTemplate, descriptions);
                    }
                }
            }

            return descriptions;
        }

        private void PopulateActionDescriptions(IEnumerable<HttpActionDescriptor> actionDescriptors, string actionVariableValue, IHttpRoute route, string localPath, Collection<ApiDescription> apiDescriptions)
        {
            foreach (HttpActionDescriptor actionDescriptor in actionDescriptors)
            {
                if (ShouldExploreAction(actionVariableValue, actionDescriptor, route))
                {
                    // exclude actions that are marked with route attributes except for the inherited actions.
                    if (!actionDescriptor.IsAttributeRouted())
                    {
                        PopulateActionDescriptions(actionDescriptor, route, localPath, apiDescriptions);
                    }
                }
            }
        }

        private void PopulateActionDescriptions(HttpActionDescriptor actionDescriptor, IHttpRoute route, string localPath, Collection<ApiDescription> apiDescriptions)
        {
            var apiDocumentation = (string) this.CallBasePrivateMethod("GetApiDocumentation", new object[] {actionDescriptor});

            var parsedRoute = MethodHelper.InvokeStaticMethod("System.Web.Http.Routing.RouteParser", "Parse",
                new object[] {localPath});

            // parameters
            var parameterDescriptions =
                (IList<ApiParameterDescription>)
                    this.CallBasePrivateMethod("CreateParameterDescriptions",
                        new object[] {actionDescriptor, parsedRoute, route.Defaults});

            // expand all parameter variables
            string finalPath;

            if (!TryExpandUriParameters(route, parsedRoute, parameterDescriptions, out finalPath))
            {
                // the action cannot be reached due to parameter mismatch, e.g. routeTemplate = "/users/{name}" and GetUsers(id)
                return;
            }

            // request formatters
            ApiParameterDescription bodyParameter = parameterDescriptions.FirstOrDefault(description => description.Source == ApiParameterSource.FromBody);
            IEnumerable<MediaTypeFormatter> supportedRequestBodyFormatters = bodyParameter != null ?
                actionDescriptor.Configuration.Formatters.Where(f => f.CanReadType(bodyParameter.ParameterDescriptor.ParameterType)) :
                Enumerable.Empty<MediaTypeFormatter>();

            // response formatters
            var responseDescription = (ResponseDescription) this.CallBasePrivateMethod("CreateResponseDescription", new object[] { actionDescriptor });
            Type returnType = responseDescription.ResponseType ?? responseDescription.DeclaredType;
            IEnumerable<MediaTypeFormatter> supportedResponseFormatters = (returnType != null && returnType != typeof(void)) ?
                actionDescriptor.Configuration.Formatters.Where(f => f.CanWriteType(returnType)) :
                Enumerable.Empty<MediaTypeFormatter>();

            // Replacing the formatter tracers with formatters if tracers are present.
            supportedRequestBodyFormatters = (IEnumerable<MediaTypeFormatter>) this.CallBasePrivateMethod("GetInnerFormatters", new object[] {supportedRequestBodyFormatters});
            supportedResponseFormatters = (IEnumerable<MediaTypeFormatter>) this.CallBasePrivateMethod("GetInnerFormatters", new object[] {supportedResponseFormatters});

            // get HttpMethods supported by an action. Usually there is one HttpMethod per action but we allow multiple of them per action as well.
            IList<HttpMethod> supportedMethods = GetHttpMethodsSupportedByAction(route, actionDescriptor);
            
            foreach (HttpMethod method in supportedMethods)
            {
                var apiDescription = new ApiDescription
                {
                    Documentation = apiDocumentation,
                    HttpMethod = method,
                    RelativePath = finalPath,
                    ActionDescriptor = actionDescriptor,
                    Route = route
                };

                apiDescription.CallPrivateMethod("set_SupportedResponseFormatters",
                    new object[] {new Collection<MediaTypeFormatter>(supportedResponseFormatters.ToList())});
                apiDescription.CallPrivateMethod("set_SupportedRequestBodyFormatters",
                    new object[] {new Collection<MediaTypeFormatter>(supportedRequestBodyFormatters.ToList())});
                apiDescription.CallPrivateMethod("set_ParameterDescriptions",
                    new object[] {new Collection<ApiParameterDescription>(parameterDescriptions)});
                apiDescription.CallPrivateMethod("set_ResponseDescription", new object[] {responseDescription});

                apiDescriptions.Add(apiDescription);
                
            }
             
        }

        internal bool TryExpandUriParameters(IHttpRoute route, object parsedRoute, ICollection<ApiParameterDescription> parameterDescriptions, out string expandedRouteTemplate)
        {
            var parameterValuesForRoute = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var emitPrefixes = (bool) this.CallBasePrivateMethod("ShouldEmitPrefixes", new object[] { parameterDescriptions });
            string prefix = String.Empty;
            foreach (ApiParameterDescription parameterDescriptor in parameterDescriptions)
            {
                if (parameterDescriptor.Source == ApiParameterSource.FromUri)
                {
                    if (parameterDescriptor.ParameterDescriptor == null ||
                        (parameterDescriptor.ParameterDescriptor != null &&
                        TypeHelper.CanConvertFromString(parameterDescriptor.ParameterDescriptor.ParameterType)))
                    {
                        if (!parameterValuesForRoute.ContainsKey(parameterDescriptor.Name))
                        {
                            parameterValuesForRoute.Add(parameterDescriptor.Name, "{" + parameterDescriptor.Name + "}");
                        }
                    }
                    else if (parameterDescriptor.ParameterDescriptor != null &&
                             parameterDescriptor.CanConvertPropertiesFromString())
                    {
                        if (emitPrefixes)
                        {
                            prefix = parameterDescriptor.Name + ".";
                        }

                        // Inserting the individual properties of the object in the query string
                        // as all the complex object can not be converted from string, but all its
                        // individual properties can.
                        foreach (PropertyInfo property in parameterDescriptor.GetBindableProperties())
                        {
                            string queryParameterName = prefix + property.Name;
                            if (!parameterValuesForRoute.ContainsKey(queryParameterName))
                            {
                                parameterValuesForRoute.Add(queryParameterName, "{" + queryParameterName + "}");
                            }
                        }
                    }

                    // for all other parameters, just treat as string
                    if (!parameterValuesForRoute.ContainsKey(parameterDescriptor.Name))
                    {
                        parameterValuesForRoute.Add(parameterDescriptor.Name, "{" + parameterDescriptor.Name + "}");
                    }
                }
            }

            var boundRouteTemplate = parsedRoute.CallPrivateMethod("Bind", new object[] { null, parameterValuesForRoute, new HttpRouteValueDictionary(route.Defaults), new HttpRouteValueDictionary(route.Constraints) });
            if (boundRouteTemplate == null)
            {
                expandedRouteTemplate = null;
                return false;
            }

            expandedRouteTemplate = Uri.UnescapeDataString((string) boundRouteTemplate.GetType().GetProperty("BoundTemplate").GetValue(boundRouteTemplate));
            return true;
        }

        private Collection<ApiDescription> ExploreRouteControllers(IDictionary<string, HttpControllerDescriptor> controllerMappings, IHttpRoute route)
        {
            var apiDescriptions = new Collection<ApiDescription>();
            string routeTemplate = route.RouteTemplate;
            string controllerVariableValue;
            if (_controllerVariableRegex.IsMatch(routeTemplate))
            {
                // unbound controller variable, {controller}
                foreach (KeyValuePair<string, HttpControllerDescriptor> controllerMapping in controllerMappings)
                {
                    controllerVariableValue = controllerMapping.Key;
                    HttpControllerDescriptor controllerDescriptor = controllerMapping.Value;
                    if (ShouldExploreController(controllerVariableValue, controllerDescriptor, route))
                    {
                        // expand {controller} variable
                        string expandedRouteTemplate = _controllerVariableRegex.Replace(routeTemplate, controllerVariableValue);
                        ExploreRouteActions(route, expandedRouteTemplate, controllerDescriptor, apiDescriptions);
                    }
                }
            }
            else if (route.Defaults.TryGetValue(RouteValueKeys.Controller, out controllerVariableValue))
            {
                // bound controller variable, {controller = "controllerName"}
                HttpControllerDescriptor controllerDescriptor;
                if (controllerMappings.TryGetValue(controllerVariableValue, out controllerDescriptor) && ShouldExploreController(controllerVariableValue, controllerDescriptor, route))
                {
                    ExploreRouteActions(route, routeTemplate, controllerDescriptor, apiDescriptions);
                }
            }

            return apiDescriptions;
        }

        private void ExploreRouteActions(IHttpRoute route, string localPath, HttpControllerDescriptor controllerDescriptor, Collection<ApiDescription> apiDescriptions)
        {
            // exclude controllers that are marked with route attributes.
            if (!controllerDescriptor.IsAttributeRouted())
            {
                ServicesContainer controllerServices = controllerDescriptor.Configuration.Services;
                ILookup<string, HttpActionDescriptor> actionMappings = controllerServices.GetActionSelector().GetActionMapping(controllerDescriptor);
                string actionVariableValue;
                if (actionMappings != null)
                {
                    if (_actionVariableRegex.IsMatch(localPath))
                    {
                        // unbound action variable, {action}
                        foreach (IGrouping<string, HttpActionDescriptor> actionMapping in actionMappings)
                        {
                            // expand {action} variable
                            actionVariableValue = actionMapping.Key;
                            string expandedLocalPath = _actionVariableRegex.Replace(localPath, actionVariableValue);
                            PopulateActionDescriptions(actionMapping, actionVariableValue, route, expandedLocalPath, apiDescriptions);
                        }
                    }
                    else if (route.Defaults.TryGetValue(RouteValueKeys.Action, out actionVariableValue))
                    {
                        // bound action variable, { action = "actionName" }
                        PopulateActionDescriptions(actionMappings[actionVariableValue], actionVariableValue, route, localPath, apiDescriptions);
                    }
                    else
                    {
                        // no {action} specified, e.g. {controller}/{id}
                        foreach (IGrouping<string, HttpActionDescriptor> actionMapping in actionMappings)
                        {
                            PopulateActionDescriptions(actionMapping, null, route, localPath, apiDescriptions);
                        }
                    }
                }
            }
        }

        private sealed class ApiDescriptionComparer : IEqualityComparer<ApiDescription>
        {
            public bool Equals(ApiDescription x, ApiDescription y)
            {
                return String.Equals(x.ID, y.ID, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(ApiDescription obj)
            {
                return obj.ID.ToUpperInvariant().GetHashCode();
            }
        }
  
    }
}