namespace IIGO.Authentication
{
    /// <summary>
    /// Adds origins to the Content-Security-Policy form-action directive.
    /// </summary>
    /// <remarks>
    /// The sign-in page posts to the panel, which then redirects the browser to the external provider. Browsers check
    /// that redirect against form-action, which otherwise only allows the panel itself. A plugin adding a provider
    /// registers one of these (as a singleton) returning the provider's sign-in origin, such as
    /// "https://accounts.google.com". It is asked on every request, so it can follow settings that change at run time.
    /// Only plain https origins are used; anything else is ignored.
    /// </remarks>
    public interface IFormActionSourceProvider
    {
        IEnumerable<string> GetFormActionSources();
    }
}
