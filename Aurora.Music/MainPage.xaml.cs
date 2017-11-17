﻿using System;
using Aurora.Music.Pages;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Aurora.Music.ViewModels;
using Aurora.Shared.Controls;
using Windows.UI.Input;
using Windows.UI.Xaml.Media.Animation;
using Aurora.Music.Core;
using Windows.UI.Xaml.Media;
using Aurora.Shared;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Aurora.Shared.Extensions;
using Aurora.Music.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;
using System.Collections.Generic;
using Aurora.Music.Core.Models;
using System.Threading.Tasks;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Aurora.Music
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page, IChangeTheme
    {
        public static MainPage Current;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
            MainFrame.Navigate(typeof(HomePage));
            GestureRecognizer g = new GestureRecognizer
            {
                GestureSettings = GestureSettings.HoldWithMouse
            };
        }

        private Type[] navigateOptions = { typeof(HomePage), typeof(LibraryPage) };

        public bool CanGoBack { get => MainFrame.Visibility == Visibility.Visible; }

        private void Main_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            MainFrame.Navigate(navigateOptions[sender.MenuItems.IndexOf(args.SelectedItem)]);
        }

        string PositionToString(TimeSpan t1, TimeSpan total)
        {
            if (total == null || total == default(TimeSpan))
            {
                return "0:00/0:00";
            }
            return (t1.ToString(@"m\:ss") + '/' + total.ToString(@"m\:ss"));
        }

        public void Navigate(Type type)
        {
            if (OverlayFrame.Visibility == Visibility.Visible)
                return;
            MainFrame.Navigate(type);
        }

        public void Navigate(Type type, object parameter)
        {
            if (OverlayFrame.Visibility == Visibility.Visible)
                return;
            MainFrame.Navigate(type, parameter);
        }

        private void Toggle_PaneOpened(object sender, RoutedEventArgs e) => Root.IsPaneOpen = !Root.IsPaneOpen;

        public SolidColorBrush TitleForeground(bool b) => (SolidColorBrush)(b ? Resources["SystemControlForegroundAltHighBrush"] : Resources["SystemControlForegroundBaseHighBrush"]);

        private void Pane_CurrentChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayFrame.Visibility == Visibility.Visible)
            {
                GoBackFromNowPlaying();
            }

            if (((sender as ListView).SelectedItem as HamPanelItem) == Context.HamList.Find(x => x.IsCurrent))
            {
                Root.IsPaneOpen = false;
                return;
            }

            foreach (var item in Context.HamList)
            {
                item.IsCurrent = false;
            }
            ((sender as ListView).SelectedItem as HamPanelItem).IsCurrent = true;
            MainFrame.Navigate(((sender as ListView).SelectedItem as HamPanelItem).TargetType);
            Root.IsPaneOpen = false;
        }

        public void ChangeTheme()
        {
            if (MainFrame.Content is IChangeTheme iT)
            {
                iT.ChangeTheme();
            }
            var ui = new UISettings();
            Context.IsDarkAccent = Palette.IsDarkColor(ui.GetColorValue(UIColorType.Accent));
        }

        private void FastFoward_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Canceled)
            {
                Context.FastForward(false);
            }
            else
            {
                Context.FastForward(true);
            }
        }

        private void StackPanel_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Context.NowPlayingList.Count > 0 && Context.CurrentIndex >= 0)
            {
                OverlayFrame.Visibility = Visibility.Visible;
                MainFrame.Visibility = Visibility.Collapsed;
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(Consts.NowPlayingPageInAnimation, Artwork);
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate($"{Consts.NowPlayingPageInAnimation}_1", Title);
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate($"{Consts.NowPlayingPageInAnimation}_2", Album).Completed += MainPage_Completed; ;
                OverlayFrame.Navigate(typeof(NowPlayingPage), Context.NowPlayingList[Context.CurrentIndex]);
            }
            if (sender is Panel s)
            {
                (s.Resources["PointerOver"] as Storyboard).Begin();
                e.Handled = true;
            }
        }

        private void MainPage_Completed(ConnectedAnimation sender, object args)
        {
            NowPanel.Visibility = Visibility.Collapsed;
            sender.Completed -= MainPage_Completed;
        }

        public void GoBackFromNowPlaying()
        {
            if (OverlayFrame.Visibility == Visibility.Visible)
            {
                NowPanel.Visibility = Visibility.Visible;
                MainFrame.Visibility = Visibility.Visible;
                OverlayFrame.Content = null;
                var ani = ConnectedAnimationService.GetForCurrentView().GetAnimation(Consts.NowPlayingPageInAnimation);
                if (ani != null)
                {
                    ani.TryStart(Artwork, new UIElement[] { Root });
                }
                ani = ConnectedAnimationService.GetForCurrentView().GetAnimation($"{Consts.NowPlayingPageInAnimation}_1");
                if (ani != null)
                {
                    ani.TryStart(Title);
                }
                ani = ConnectedAnimationService.GetForCurrentView().GetAnimation($"{Consts.NowPlayingPageInAnimation}_2");
                if (ani != null)
                {
                    ani.TryStart(Album);

                    ani.Completed += Ani_Completed;
                }
                else
                {
                    OverlayFrame.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Ani_Completed(ConnectedAnimation sender, object args)
        {
            sender.Completed -= Ani_Completed;
            OverlayFrame.Visibility = Visibility.Collapsed;
        }

        private void StackPanel_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Panel s)
            {
                (s.Resources["PointerOver"] as Storyboard).Begin();
                e.Handled = true;
            }
        }

        private void StackPanel_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Panel s)
            {
                (s.Resources["Normal"] as Storyboard).Begin();
                e.Handled = true;

                s.SetValue(RevealBrush.StateProperty, RevealBrushState.Normal);
            }
        }

        private void NavigatePanel_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Panel s)
            {
                (s.Resources["PointerPressed"] as Storyboard).Begin();
                e.Handled = true;
                s.SetValue(RevealBrush.StateProperty, RevealBrushState.Pressed);
            }
        }

        private void TitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            // Get the size of the caption controls area and back button 
            // (returned in logical pixels), and move your content around as necessary.
            SearchBox.Margin = new Thickness(0, 0, coreTitleBar.SystemOverlayRightInset, 0);
            TitlebarBtm.Width = coreTitleBar.SystemOverlayRightInset;
            // Update title bar control size as needed to account for system size changes.
            TitleBar.Height = coreTitleBar.Height;
            TitleBarOverlay.Height = coreTitleBar.Height;

            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;

            Window.Current.SetTitleBar(TitleBar);
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                TitleBar.Visibility = Visibility.Visible;
                TitlebarBtm.Visibility = Visibility.Visible;
                SearchBox.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);
            }
            else
            {
                TitleBar.Visibility = Visibility.Collapsed;
                TitlebarBtm.Visibility = Visibility.Collapsed;
                SearchBox.Margin = new Thickness(0);
            }

        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            // Get the size of the caption controls area and back button 
            // (returned in logical pixels), and move your content around as necessary.
            if (sender.IsVisible)
            {
                SearchBox.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);
            }
            else
            {
                SearchBox.Margin = new Thickness(0);
            }
            // Update title bar control size as needed to account for system size changes.
            TitlebarBtm.Width = sender.SystemOverlayRightInset;
            TitleBar.Height = sender.Height;
            TitleBarOverlay.Height = sender.Height;
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is GenericMusicItemViewModel g)
            {
                var dialog = new SearchResultDialog(g);
                var result = await dialog.ShowAsync();
            }
            else
            {
                if (Context.SearchItems.IsNullorEmpty())
                {
                    var dialog = new SearchResultDialog();
                    var result = await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new SearchResultDialog(Context.SearchItems[0]);
                    var result = await dialog.ShowAsync();
                }
            }
            sender.Text = string.Empty;
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {

        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }
            if (sender.Text.IsNullorWhiteSpace())
            {
                Context.SearchItems.Clear();
                return;
            }
            var text = sender.Text;
            var t = ThreadPool.RunAsync(async x =>
            {
                await Context.Search(text);
            });
        }

        private void Grid_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Panel s)
            {
                (s.Resources["PointerOver"] as Storyboard).Begin();
            }
        }

        private void Grid_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Panel s)
            {
                (s.Resources["Normal"] as Storyboard).Begin();
            }
        }

        private void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            Context.SkiptoItem((sender as Button).DataContext as SongViewModel);
        }

        private async void Root_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;

            e.DragUIOverride.SetContentFromBitmapImage(new BitmapImage(new Uri(Consts.BlackPlaceholder)));

            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            var p = await e.DataView.GetStorageItemsAsync();
            if (p.Count > 0)
            {
                e.DragUIOverride.IsGlyphVisible = true;
                e.DragUIOverride.Caption = "Drop to Play";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
            }
            else
            {
                e.DragUIOverride.IsGlyphVisible = true;
                e.DragUIOverride.Caption = "Can't Drop Here";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = false;
            }
        }

        private async void Root_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            ShowModalUI(true, "Loading Files");
            var p = await e.DataView.GetStorageItemsAsync();
            var list = new List<StorageFile>();
            if (p.Count > 0)
            {
                list.AddRange(await CopyFilesAsync(p));
            }
            else
            {
                return;
            }
            var songs = await Context.ComingNewSongsAsync(list);

            await Context.InstantPlay(songs);

            if (songs.Count > 0)
            {
                ShowDropSongsUI(songs);
            }
        }

        private async void ShowDropSongsUI(IList<Song> songs)
        {
            ShowModalUI(false, string.Empty);
            var dialog = new DropSongsDialog(songs);
            await dialog.ShowAsync();
        }

        public async Task FileActivated(IReadOnlyList<IStorageItem> p)
        {
            var list = new List<StorageFile>();
            if (p.Count > 0)
            {
                list.AddRange(await CopyFilesAsync(p));
            }
            else
            {
                return;
            }

            ShowModalUI(true, "Loading Files");

            var songs = await Context.ComingNewSongsAsync(list);

            await Context.InstantPlay(songs);

            if (songs.Count > 0)
            {
                ShowDropSongsUI(songs);
            }
        }

        private void ShowModalUI(bool v1, string v2)
        {
            if (v1)
            {
                ModalIn.Begin();
            }
            else
            {
                ModalOut.Begin();
            }
            ModalText.Text = v2;
        }

        private static async System.Threading.Tasks.Task<IReadOnlyList<StorageFile>> CopyFilesAsync(IReadOnlyList<IStorageItem> p)
        {
            var list = new List<StorageFile>();
            foreach (var item in p)
            {
                if (item is IStorageFile file)
                {
                    foreach (var types in Consts.FileTypes)
                    {
                        if (types == file.FileType)
                        {
                            list.Add(await file.CopyAsync(await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("Songs", CreationCollisionOption.OpenIfExists), file.Name, NameCollisionOption.ReplaceExisting));
                            break;
                        }
                    }
                }
                else if (item is StorageFolder folder)
                {
                    var options = new Windows.Storage.Search.QueryOptions
                    {
                        FileTypeFilter = { ".flac", ".wav", ".m4a", ".aac", ".mp3", ".wma" },
                        FolderDepth = Windows.Storage.Search.FolderDepth.Deep,
                        IndexerOption = Windows.Storage.Search.IndexerOption.DoNotUseIndexer,
                    };
                    var query = folder.CreateFileQueryWithOptions(options);
                    list.AddRange(await query.GetFilesAsync());
                }
            }
            return list;
        }
    }
}
