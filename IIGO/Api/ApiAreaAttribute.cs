using IIGO.Services;

namespace IIGO.Api
{
    /// <summary>
    /// The <see cref="ApiScopes"/> area a controller or action belongs to. GET requests need read access to it
    /// and other methods need write access, unless the action has <see cref="ApiAccessLevelAttribute"/>.
    /// Endpoints without an area need a full-access key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public class ApiAreaAttribute(string area) : Attribute
    {
        public string Area { get; } = area;
    }

    /// <summary>
    /// The access level an action needs, in place of the default from its HTTP method. Use
    /// <see cref="ApiAccess.Operate"/> for actions such as start, stop and recycle that don't create, change or delete anything.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ApiAccessLevelAttribute(ApiAccess access) : Attribute
    {
        public ApiAccess Access { get; } = access;
    }
}
