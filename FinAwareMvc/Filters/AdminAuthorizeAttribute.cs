using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;

namespace FinAwareMvc.Filters
{
    public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var token = context.HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var role = jwt.Claims
                    .FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?.Value;

                if (role != "Admin")
                    context.Result = new RedirectToActionResult("Index", "Home", null);
            }
            catch
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
            }
        }
    }
}