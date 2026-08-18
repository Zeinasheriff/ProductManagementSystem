using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

namespace ProductManagement.Blazor.Services;

public class BaseHandler
{
    protected readonly HttpClient _httpClient;
    protected readonly ILocalStorageService _localStorage;
    protected readonly AuthenticationStateProvider _authStateProvider;

    public BaseHandler(HttpClient httpClient, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }
}