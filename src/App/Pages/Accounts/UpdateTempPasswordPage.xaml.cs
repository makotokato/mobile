using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using Bit.App.Resources;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class UpdateTempPasswordPage : BaseContentPage
    {
        private readonly IMessagingService _messagingService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly UpdateTempPasswordPageViewModel _vm;

        public UpdateTempPasswordPage()
        {  
            // Service Init
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            
            // Service Use
            _messagingService.Send("showStatusBar", true);
            
            // Binding
            InitializeComponent();
            _vm = BindingContext as UpdateTempPasswordPageViewModel;
            _vm.Page = this;
            
            // Actions Declaration
            _vm.LogOutAction = () =>
            {
                _messagingService.Send("logout");
            };
            _vm.UpdateTempPasswordSuccessAction = () => Device.BeginInvokeOnMainThread(UpdateTempPasswordSuccess);
            
            // Link fields that will be referenced in codebehind
            MasterPasswordEntry = _masterPassword;
            ConfirmMasterPasswordEntry = _confirmMasterPassword;
            
            // Return Types and Commands
            _masterPassword.ReturnType = ReturnType.Next;
            _masterPassword.ReturnCommand = new Command(() => _confirmMasterPassword.Focus());
            _confirmMasterPassword.ReturnType = ReturnType.Next;
            _confirmMasterPassword.ReturnCommand = new Command(() => _hint.Focus());
        }

        public Entry MasterPasswordEntry { get; set; }
        public Entry ConfirmMasterPasswordEntry { get; set; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitAsync();
            RequestFocus(_masterPassword);
        }

        private async void Submit_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.SubmitAsync();
            }
        }

        private async void LogOut_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.LogoutConfirmation,
                    AppResources.LogOut, AppResources.Yes, AppResources.Cancel);
                if (confirmed)
                {
                    _vm.LogOutAction();
                }
            }
        }
        
        private void UpdateTempPasswordSuccess()
        {
            _messagingService.Send("logout");
        }
    }
}
