// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.toggle-password').forEach(function (boton) {
        boton.addEventListener('click', function () {
            var input = document.getElementById(boton.dataset.target);
            if (!input) return;
            var oculto = input.type === 'password';
            input.type = oculto ? 'text' : 'password';
            boton.textContent = oculto ? '🙈' : '👁';
        });
    });
});
