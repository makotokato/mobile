﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Bit.App.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class LoginPageViewModel : CaptchaProtectedViewModel
    {
        private const string Keys_RememberedEmail = "rememberedEmail";
        private const string Keys_RememberEmail = "rememberEmail";

        private readonly IDeviceActionService _deviceActionService;
        private readonly IAuthService _authService;
        private readonly ISyncService _syncService;
        private readonly IStorageService _storageService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IStateService _stateService;
        private readonly IEnvironmentService _environmentService;
        private readonly II18nService _i18nService;

        private bool _showPassword;
        private string _email;
        private string _masterPassword;

        public LoginPageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _authService = ServiceContainer.Resolve<IAuthService>("authService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _environmentService = ServiceContainer.Resolve<IEnvironmentService>("environmentService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");

            PageTitle = AppResources.Bitwarden;
            TogglePasswordCommand = new Command(TogglePassword);
            LogInCommand = new Command(async () => await LogInAsync());
        }

        public bool ShowPassword
        {
            get => _showPassword;
            set => SetProperty(ref _showPassword, value,
                additionalPropertyNames: new string[]
                {
                    nameof(ShowPasswordIcon)
                });
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string MasterPassword
        {
            get => _masterPassword;
            set => SetProperty(ref _masterPassword, value);
        }

        public Command LogInCommand { get; }
        public Command TogglePasswordCommand { get; }
        public string ShowPasswordIcon => ShowPassword ? "" : "";
        public bool RememberEmail { get; set; }
        public Action StartTwoFactorAction { get; set; }
        public Action LogInSuccessAction { get; set; }
        public Action UpdateTempPasswordAction { get; set; }
        public Action CloseAction { get; set; }

        protected override II18nService i18nService => _i18nService;
        protected override IEnvironmentService environmentService => _environmentService;
        protected override IDeviceActionService deviceActionService => _deviceActionService;
        protected override IPlatformUtilsService platformUtilsService => _platformUtilsService;

        public async Task InitAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                Email = await _storageService.GetAsync<string>(Keys_RememberedEmail);
            }
            var rememberEmail = await _storageService.GetAsync<bool?>(Keys_RememberEmail);
            RememberEmail = rememberEmail.GetValueOrDefault(true);
        }

        public async Task LogInAsync(bool showLoading = true)
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle, AppResources.Ok);
                return;
            }
            if (string.IsNullOrWhiteSpace(Email))
            {
                await _platformUtilsService.ShowDialogAsync(
                    string.Format(AppResources.ValidationFieldRequired, AppResources.EmailAddress),
                    AppResources.AnErrorHasOccurred, AppResources.Ok);
                return;
            }
            if (!Email.Contains("@"))
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InvalidEmail, AppResources.AnErrorHasOccurred,
                    AppResources.Ok);
                return;
            }
            if (string.IsNullOrWhiteSpace(MasterPassword))
            {
                await _platformUtilsService.ShowDialogAsync(
                    string.Format(AppResources.ValidationFieldRequired, AppResources.MasterPassword),
                    AppResources.AnErrorHasOccurred, AppResources.Ok);
                return;
            }

            ShowPassword = false;
            try
            {
                if (showLoading)
                {
                    await _deviceActionService.ShowLoadingAsync(AppResources.LoggingIn);
                }

                var response = await _authService.LogInAsync(Email, MasterPassword, _captchaToken);
                if (RememberEmail)
                {
                    await _storageService.SaveAsync(Keys_RememberedEmail, Email);
                }
                else
                {
                    await _storageService.RemoveAsync(Keys_RememberedEmail);
                }
                await AppHelpers.ResetInvalidUnlockAttemptsAsync();

                if (response.CaptchaNeeded)
                {
                    if (await HandleCaptchaAsync(response.CaptchaSiteKey))
                    {
                        await LogInAsync(false);
                        _captchaToken = null;
                    }
                    return;
                }
                MasterPassword = string.Empty;
                _captchaToken = null;

                await _deviceActionService.HideLoadingAsync();

                if (response.TwoFactor)
                {
                    StartTwoFactorAction?.Invoke();
                }
                else if (response.ForcePasswordReset)
                {
                    UpdateTempPasswordAction?.Invoke();
                }
                else
                {
                    var disableFavicon = await _storageService.GetAsync<bool?>(Constants.DisableFaviconKey);
                    await _stateService.SaveAsync(Constants.DisableFaviconKey, disableFavicon.GetValueOrDefault());
                    var task = Task.Run(async () => await _syncService.FullSyncAsync(true));
                    LogInSuccessAction?.Invoke();
                }
            }
            catch (ApiException e)
            {
                _captchaToken = null;
                MasterPassword = string.Empty;
                await _deviceActionService.HideLoadingAsync();
                if (e?.Error != null)
                {
                    await _platformUtilsService.ShowDialogAsync(e.Error.GetSingleMessage(),
                        AppResources.AnErrorHasOccurred, AppResources.Ok);
                }
            }
        }

        public void TogglePassword()
        {
            ShowPassword = !ShowPassword;
            (Page as LoginPage).MasterPasswordEntry.Focus();
        }
    }
}
