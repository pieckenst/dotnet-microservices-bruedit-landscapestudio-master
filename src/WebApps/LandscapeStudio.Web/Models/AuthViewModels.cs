using System.ComponentModel.DataAnnotations;

namespace LandscapeStudio.Web.Models;

public sealed class LoginViewModel
{
    [Required, StringLength(80, MinimumLength = 3)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed record ErrorViewModel(string RequestId)
{
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}

public sealed record LoginRequest(string UserName, string Password);
