using IIGO.Data;
using Microsoft.AspNetCore.Identity;

namespace IIGO.Authentication
{
    /// <summary>
    /// Decides who may sign in through an external login provider, such as Google Workspace.
    /// </summary>
    /// <remarks>
    /// A plugin adds a provider with <c>services.AddAuthentication().AddOpenIdConnect(...)</c> (or AddGoogle and so on),
    /// gives the scheme a display name so it shows on the sign-in page, calls
    /// <see cref="ExternalLogins.HandleRemoteFailures"/> on its options, and registers one of these
    /// (<c>services.AddScoped&lt;IExternalLoginHandler, MyHandler&gt;()</c>).
    /// <para>
    /// After the provider signs someone in, the panel looks for a panel user already linked to that external account.
    /// If there is none, it asks the handler for the provider to <see cref="ResolveUserAsync"/>. Without a handler,
    /// only accounts that are already linked can sign in. Either way <see cref="ValidateSignInAsync"/> runs before
    /// every sign-in, so a handler can turn away users who no longer qualify.
    /// </para>
    /// </remarks>
    public interface IExternalLoginHandler
    {
        /// <summary>True for the authentication schemes this handler is responsible for</summary>
        bool Handles(string loginProvider);

        /// <summary>
        /// Called when no panel user is linked to this external account yet. Return
        /// <see cref="ExternalLoginResolution.Link"/> to link an existing user, <see cref="ExternalLoginResolution.Create"/>
        /// to create one, or <see cref="ExternalLoginResolution.Deny"/>.
        /// Only link by email address when the provider has verified it and the domain is one you trust.
        /// </summary>
        Task<ExternalLoginResolution> ResolveUserAsync(ExternalLoginInfo info);

        /// <summary>
        /// Called before every sign-in through this provider, including linked users.
        /// Return null to allow it, or a message to show the user instead.
        /// </summary>
        Task<string?> ValidateSignInAsync(ApplicationUser user, ExternalLoginInfo info) => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// What to do with an external account that isn't linked to a panel user yet
    /// </summary>
    public sealed class ExternalLoginResolution
    {
        private ExternalLoginResolution(ApplicationUser? user, bool create, IReadOnlyList<string> roles, string? error)
        {
            User = user;
            CreateUser = create;
            Roles = roles;
            Error = error;
        }

        public ApplicationUser? User { get; }
        public bool CreateUser { get; }
        public IReadOnlyList<string> Roles { get; }
        public string? Error { get; }

        /// <summary>Link the external account to this existing user</summary>
        public static ExternalLoginResolution Link(ApplicationUser user) => new(user, false, [], null);

        /// <summary>
        /// Create this user, without a password, in these roles, and link the external account to it.
        /// Set <see cref="ApplicationUser.EmailConfirmed"/> to true, or the user won't be allowed to sign in.
        /// </summary>
        public static ExternalLoginResolution Create(ApplicationUser user, IEnumerable<string> roles) => new(user, true, [.. roles], null);

        /// <summary>Refuse the sign-in with this message</summary>
        public static ExternalLoginResolution Deny(string message) => new(null, false, [], message);
    }
}
