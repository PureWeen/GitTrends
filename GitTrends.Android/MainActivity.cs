﻿using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Autofac;
using Plugin.CurrentActivity;

namespace GitTrends.Droid
{
    [Activity(Label = "GitTrends", Icon = "@mipmap/icon", RoundIcon = "@mipmap/icon_round", Theme = "@style/LaunchTheme", LaunchMode = LaunchMode.SingleTop, MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    [IntentFilter(new string[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable }, DataSchemes = new[] { "gittrends" })]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

#if !AppStore
        #region UI Test Back Door Methods
        [Preserve, Java.Interop.Export(Mobile.Shared.BackdoorMethodConstants.SetGitHubUser)]
        public async void SetGitHubUser(string accessToken)
        {
            using var scope = ContainerService.Container.BeginLifetimeScope();
            var backdoorService = scope.Resolve<UITestBackdoorService>();

            await backdoorService.SetGitHubUser(accessToken.ToString()).ConfigureAwait(false);
        }

        [Preserve, Java.Interop.Export(Mobile.Shared.BackdoorMethodConstants.TriggerPullToRefresh)]
        public async void TriggerRepositoriesPullToRefresh()
        {
            using var scope = ContainerService.Container.BeginLifetimeScope();
            var backdoorService = scope.Resolve<UITestBackdoorService>();

            await backdoorService.TriggerPullToRefresh().ConfigureAwait(false);
        }

        [Preserve, Java.Interop.Export(Mobile.Shared.BackdoorMethodConstants.GetVisibleRepositoryList)]
        public string GetVisibleRepositoryList()
        {
            using var scope = ContainerService.Container.BeginLifetimeScope();
            var backdoorService = scope.Resolve<UITestBackdoorService>();

            return Newtonsoft.Json.JsonConvert.SerializeObject(backdoorService.GetVisibleRepositoryList());
        }

        [Preserve, Java.Interop.Export(Mobile.Shared.BackdoorMethodConstants.GetVisibleReferringSitesList)]
        public string GetVisibleReferringSitesList()
        {
            using var scope = ContainerService.Container.BeginLifetimeScope();
            var backdoorService = scope.Resolve<UITestBackdoorService>();

            return Newtonsoft.Json.JsonConvert.SerializeObject(backdoorService.GetVisibleReferringSitesList());
        }
        #endregion
#endif

        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.SetTheme(Resource.Style.MainTheme);
            base.OnCreate(savedInstanceState);

            CrossCurrentActivity.Current.Init(this, savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

            FFImageLoading.Forms.Platform.CachedImageRenderer.Init(true);
            FFImageLoading.Forms.Platform.CachedImageRenderer.InitImageViewHandler();
            var ignore = typeof(FFImageLoading.Svg.Forms.SvgCachedImage);

            var app = new App();

            if (Intent?.Data is Android.Net.Uri callbackUri)
            {
                //Wait for Application.MainPage to load before handling the callbackUri
                app.PageAppearing += HandlePageAppearing;
            }

            LoadApplication(app);

            async void HandlePageAppearing(object sender, Xamarin.Forms.Page page)
            {
                if (page is SettingsPage)
                {
                    app.PageAppearing -= HandlePageAppearing;
                    await AuthorizeGitHubSession(callbackUri).ConfigureAwait(false);
                }
                else if (page is RepositoryPage)
                {
                    await NavigateToSettingsPage().ConfigureAwait(false);
                }
            }
        }

        protected override async void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            if (intent?.Data is Android.Net.Uri callbackUri)
            {
                await NavigateToSettingsPage().ConfigureAwait(false);
                await AuthorizeGitHubSession(callbackUri).ConfigureAwait(false);
            }
        }

        static async ValueTask NavigateToSettingsPage()
        {
            var navigationPage = (Xamarin.Forms.NavigationPage)Xamarin.Forms.Application.Current.MainPage;

            if (navigationPage.CurrentPage.GetType() != typeof(SettingsPage))
            {
                using var containerScope = ContainerService.Container.BeginLifetimeScope();
                var settingsPage = containerScope.Resolve<SettingsPage>();

                await Xamarin.Essentials.MainThread.InvokeOnMainThreadAsync(() => navigateToSettingsPage(navigationPage, settingsPage)).ConfigureAwait(false);
            }

            static async Task navigateToSettingsPage(Xamarin.Forms.NavigationPage mainNavigationPage, SettingsPage settingsPage)
            {
                await mainNavigationPage.PopToRootAsync();
                await mainNavigationPage.PushAsync(settingsPage);
            }
        }

        static async Task AuthorizeGitHubSession(Android.Net.Uri callbackUri)
        {
            using var containerScope = ContainerService.Container.BeginLifetimeScope();

            try
            {
                var gitHubAuthenticationService = containerScope.Resolve<GitHubAuthenticationService>();
                await gitHubAuthenticationService.AuthorizeSession(new Uri(callbackUri.ToString())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                containerScope.Resolve<AnalyticsService>().Report(ex);
            }
        }
    }
}