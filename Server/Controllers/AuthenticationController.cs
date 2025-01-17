﻿#nullable enable
using AspNet.Security.OAuth.Gitee;
using ClickOnceGet.Shared.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ClickOnceGet.Server.Controllers;

public class AuthenticationController : Controller
{
    [HttpGet("/auth/signin")]
    public IActionResult OnGetSignIn([FromQuery] string? returnUri)
    {
        return this.Challenge(
            new AuthenticationProperties { RedirectUri = returnUri ?? "/" },
            GiteeAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpPost("/auth/signout")]
    public IActionResult OnPostSignOut()
    {
        return this.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpGet("/api/auth/currentuser")]
    public AuthUserInfo GetCurrentUser()
    {
        var name = this.User.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? this.User.Identity?.Name ?? "";
        return new AuthUserInfo { Name = name };
    }
}
