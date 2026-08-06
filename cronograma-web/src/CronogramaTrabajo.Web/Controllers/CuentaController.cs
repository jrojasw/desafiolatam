using CronogramaTrabajo.Web.Data;
using CronogramaTrabajo.Web.Models;
using CronogramaTrabajo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CronogramaTrabajo.Web.Controllers;

[AllowAnonymous]
public class CuentaController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public CuentaController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    private string DominioPermitido => _configuration["AccessControl:DominioPermitido"] ?? "copayapunos.cl";

    [HttpGet]
    public IActionResult Registrar() => View(new RegistrarViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(RegistrarViewModel modelo)
    {
        var correo = modelo.Correo.Trim().ToLowerInvariant();

        if (!correo.EndsWith("@" + DominioPermitido, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(modelo.Correo), $"Solo se permiten correos @{DominioPermitido}.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario = new ApplicationUser
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = modelo.NombreCompleto.Trim()
        };

        var resultado = await _userManager.CreateAsync(usuario, modelo.Contrasena);

        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(modelo);
        }

        var adminEmail = _configuration["AccessControl:AdminEmail"];
        if (!string.IsNullOrWhiteSpace(adminEmail) &&
            correo.Equals(adminEmail.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            if (!await _roleManager.RoleExistsAsync(IdentitySeeder.RolAdministrador))
            {
                await _roleManager.CreateAsync(new IdentityRole(IdentitySeeder.RolAdministrador));
            }
            await _userManager.AddToRoleAsync(usuario, IdentitySeeder.RolAdministrador);
        }

        await EnviarCorreoConfirmacionAsync(usuario);

        return RedirectToAction(nameof(RevisaTuCorreo));
    }

    [HttpGet]
    public IActionResult RevisaTuCorreo() => View();

    [HttpGet]
    public IActionResult AccesoDenegado() => View();

    [HttpGet]
    public async Task<IActionResult> ConfirmarCorreo(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return RedirectToAction(nameof(IniciarSesion));
        }

        var usuario = await _userManager.FindByIdAsync(userId);
        if (usuario is null)
        {
            ViewBag.Exito = false;
            return View();
        }

        var resultado = await _userManager.ConfirmEmailAsync(usuario, token);
        ViewBag.Exito = resultado.Succeeded;
        return View();
    }

    [HttpGet]
    public IActionResult IniciarSesion(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new IniciarSesionViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarSesion(IniciarSesionViewModel modelo, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var correo = modelo.Correo.Trim().ToLowerInvariant();
        var resultado = await _signInManager.PasswordSignInAsync(
            correo, modelo.Contrasena, modelo.Recordarme, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Tareas");
        }

        if (resultado.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty,
                "Debes confirmar tu correo antes de iniciar sesión. Revisa tu bandeja de entrada.");
            ViewBag.CorreoParaReenviar = correo;
            return View(modelo);
        }

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Tu cuenta está bloqueada temporalmente por demasiados intentos fallidos. Intenta más tarde.");
            return View(modelo);
        }

        ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReenviarConfirmacion(string correo)
    {
        var usuario = await _userManager.FindByEmailAsync(correo.Trim().ToLowerInvariant());
        if (usuario is not null && !await _userManager.IsEmailConfirmedAsync(usuario))
        {
            await EnviarCorreoConfirmacionAsync(usuario);
        }

        return RedirectToAction(nameof(RevisaTuCorreo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarSesion()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(IniciarSesion));
    }

    private async Task EnviarCorreoConfirmacionAsync(ApplicationUser usuario)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);
        var enlace = Url.Action(nameof(ConfirmarCorreo), "Cuenta",
            new { userId = usuario.Id, token }, protocol: Request.Scheme);

        var cuerpo = $@"
            <p>Hola {usuario.NombreCompleto},</p>
            <p>Gracias por registrarte en el Cronograma de Trabajo. Confirma tu correo haciendo clic en el siguiente enlace:</p>
            <p><a href=""{enlace}"">Confirmar mi correo</a></p>
            <p>Si no solicitaste esto, puedes ignorar este mensaje.</p>";

        await _emailSender.SendEmailAsync(usuario.Email!, "Confirma tu correo - Cronograma de Trabajo", cuerpo);
    }
}
