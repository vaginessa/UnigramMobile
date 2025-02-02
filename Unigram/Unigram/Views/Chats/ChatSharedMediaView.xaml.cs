﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Controls.Chats;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedMediaView : UserControl, INavigablePage, IFileDelegate
    {
        public ChatSharedMediaViewModel ViewModel => DataContext as ChatSharedMediaViewModel;

        public ChatSharedMediaView()
        {
            InitializeComponent();

            InitializeSearch(SearchFiles, () => new SearchMessagesFilterDocument());
            InitializeSearch(SearchLinks, () => new SearchMessagesFilterUrl());
            InitializeSearch(SearchMusic, () => new SearchMessagesFilterAudio());
            InitializeSearch(SearchVoice, () => new SearchMessagesFilterVoiceNote());

            _tabs = new ObservableCollection<ChatSharedMediaTab>();
            _tabs.Add(_mediaHeader = new ChatSharedMediaTab { Title = Strings.Resources.SharedMediaTab2 });
            _tabs.Add(_filesHeader = new ChatSharedMediaTab { Title = Strings.Resources.SharedFilesTab2 });
            _tabs.Add(_linksHeader = new ChatSharedMediaTab { Title = Strings.Resources.SharedLinksTab2 });
            _tabs.Add(_musicHeader = new ChatSharedMediaTab { Title = Strings.Resources.SharedMusicTab2 });
            _tabs.Add(_voiceHeader = new ChatSharedMediaTab { Title = Strings.Resources.SharedVoiceTab2 });

            if (Services.SettingsService.Current.Diagnostics.LoadMediaImmediately)
                LoadMedia.Visibility = Visibility.Collapsed;

            Header.ItemsSource = _tabs;
            Header.SelectedIndex = 0;
        }

        public void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        public void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        public void UpdateSharedCount(Chat chat)
        {
            ViewModel.ProtoService.Send(new SearchChatMessages(chat.Id, string.Empty, null, 0, 0, 1, new SearchMessagesFilterPhotoAndVideo(), 0), result =>
            {
                if (result is Messages messages)
                    _mediaHeader.Subtitle = messages.TotalCount.ToString();
            });

            ViewModel.ProtoService.Send(new SearchChatMessages(chat.Id, string.Empty, null, 0, 0, 1, new SearchMessagesFilterDocument(), 0), result =>
            {
                if (result is Messages messages)
                    _filesHeader.Subtitle = messages.TotalCount.ToString();
            });

            ViewModel.ProtoService.Send(new SearchChatMessages(chat.Id, string.Empty, null, 0, 0, 1, new SearchMessagesFilterUrl(), 0), result =>
            {
                if (result is Messages messages)
                    _linksHeader.Subtitle = messages.TotalCount.ToString();
            });

            ViewModel.ProtoService.Send(new SearchChatMessages(chat.Id, string.Empty, null, 0, 0, 1, new SearchMessagesFilterAudio(), 0), result =>
            {
                if (result is Messages messages)
                    _musicHeader.Subtitle = messages.TotalCount.ToString();
            });

            ViewModel.ProtoService.Send(new SearchChatMessages(chat.Id, string.Empty, null, 0, 0, 1, new SearchMessagesFilterVoiceNote(), 0), result =>
            {
                if (result is Messages messages)
                    _voiceHeader.Subtitle = messages.TotalCount.ToString();
            });
        }

        private readonly ObservableCollection<ChatSharedMediaTab> _tabs;
        private ChatSharedMediaTab _mediaHeader;
        private ChatSharedMediaTab _filesHeader;
        private ChatSharedMediaTab _linksHeader;
        private ChatSharedMediaTab _musicHeader;
        private ChatSharedMediaTab _voiceHeader;

        private bool _isLocked = false;

        private bool _isEmbedded;
        public bool IsEmbedded
        {
            get => _isEmbedded;
            set
            {
                Update(value, _isLocked);
            }
        }

        private IProfileTab _tab;
        public IProfileTab Tab
        {
            get => _tab;
            set
            {
                if (_tab != null && _tab is UserControl prev)
                {
                    prev.Loaded -= Tab_Loaded;

                    ScrollingHost.Items.RemoveAt(_tab.Index);
                    _tabs.RemoveAt(_tab.Index);
                }

                _tab = value;

                if (value != null && value is UserControl next)
                {
                    next.Loaded += Tab_Loaded;

                    var pivotItem = new PivotItem
                    {
                        Header = value.Text,
                        Content = next
                    };

                    ScrollingHost.Items.Insert(value.Index, pivotItem);

                    var tab = new ChatSharedMediaTab {Title = value.Text};
                    switch (value)
                    {
                        case Users.UserCommonChatsView uccv:
                            tab.Subtitle = uccv.GroupInCommonCount.ToString();
                            break;
                        case Supergroups.SupergroupMembersView sgmv:
                            if (ViewModel.Chat?.Type is ChatTypeBasicGroup dbasic && ViewModel.ProtoService.GetBasicGroup(dbasic.BasicGroupId) is BasicGroup dgroup)
                            {
                                tab.Subtitle = dgroup.MemberCount.ToString();
                                if (dgroup.MemberCount != sgmv.ViewModel.Members?.Count)
                                    sgmv.ViewModel.Members = new ViewModels.ChatMemberGroupedCollection(sgmv.ViewModel.ProtoService, sgmv.ViewModel.Chat.Id, string.Empty, !sgmv.ViewModel.IsEmbedded);
                            }
                            else if (ViewModel.Chat?.Type is ChatTypeSupergroup dsuper && ViewModel.ProtoService.GetSupergroup(dsuper.SupergroupId) is Supergroup dsgroup)
                                tab.Subtitle = dsgroup.MemberCount.ToString();
                            else
                                sgmv.ViewModel.PropertyChanged += SupergroupMembersViewModelPropertyChanged;
                            void SupergroupMembersViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
                            {
                                if (e.PropertyName.Equals("Members") && sender is ViewModels.Supergroups.SupergroupMembersViewModel vm)
                                {
                                    if (vm.Chat?.Type is ChatTypeBasicGroup basic && vm.ProtoService.GetBasicGroup(basic.BasicGroupId) is BasicGroup group)
                                        tab.Subtitle = group.MemberCount.ToString();
                                    else if (vm.Chat?.Type is ChatTypeSupergroup super && vm.ProtoService.GetSupergroup(super.SupergroupId) is Supergroup sgroup)
                                        tab.Subtitle = sgroup.MemberCount.ToString();
                                    sgmv.ViewModel.PropertyChanged -= SupergroupMembersViewModelPropertyChanged;
                                }
                            }
                            break;
                    }

                    _tabs.Insert(value.Index, tab);
                }
            }
        }

        private void Update(bool embedded, bool locked)
        {
            _tab?.Update(embedded, locked);

            _isEmbedded = embedded;
            _isLocked = locked;

            var previous = (float)HeaderPage.ActualWidth;
            var size = embedded ? 640 : (float)ActualWidth;

            //Header.IsBackEnabled = !embedded;
            //Header.IsBackButtonVisible = embedded ? Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed : Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Visible;
            Header.Height = embedded ? 40 : 48;
            HeaderPage.Height = embedded ? 40 : 48;
            HeaderPage.BackVisibility = embedded ? Visibility.Collapsed : Visibility.Visible;
            HeaderPanel.CornerRadius = new CornerRadius(embedded ? 8 : 0, embedded ? 8 : 0, 0, 0);
            HeaderPanel.MaxWidth = embedded ? 640 : double.PositiveInfinity;
            HeaderPanel.Margin = new Thickness(embedded ? 12 : 0, 0, embedded ? 12 : 0, 0);

            HeaderMedia.Padding = new Thickness(0, embedded ? 12 : embedded ? 12 + 8 : 8, 0, 0);
            HeaderFiles.Padding = HeaderLinks.Padding = HeaderMusic.Padding = HeaderVoice.Padding = new Thickness(0, embedded ? 12 : embedded ? 12 + 16 : 16, 0, 8);
            HeaderFiles.Radius = HeaderLinks.Radius = HeaderMusic.Radius = HeaderVoice.Radius = new CornerRadius(embedded ? 0 : 8, embedded ? 0 : 8, 8, 8);
        }

        public ScrollViewer GetScrollViewer()
        {
            var tab = _tab;
            var shift = 0;

            if (tab?.Index < 1)
            {
                shift -= 1;
            }

            switch (ScrollingHost.SelectedIndex + shift)
            {
                case 0:
                    return ScrollingMedia.GetScrollViewer();
                case 1:
                    return ScrollingFiles.GetScrollViewer();
                case 2:
                    return ScrollingLinks.GetScrollViewer();
                case 3:
                    return ScrollingMusic.GetScrollViewer();
                case 4:
                    return ScrollingVoice.GetScrollViewer();
            }

            if (ScrollingHost.SelectedIndex == tab.Index)
            {
                return tab.GetScrollViewer();
            }

            return null;
        }

        public void SetScrollMode(bool enable)
        {
            foreach (var scrollViewer in GetScrollViewers())
            {
                if (enable)
                {
                    scrollViewer.VerticalScrollMode = ScrollMode.Auto;
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    scrollViewer.ChangeView(null, 12, null, true);
                }
                else
                {
                    scrollViewer.ChangeView(null, 12, null, true);
                    scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                }

                Update(_isEmbedded, enable);
            }
        }

        private IEnumerable<ScrollViewer> GetScrollViewers()
        {
            var viewer1 = ScrollingMedia?.GetScrollViewer();
            if (viewer1 != null)
            {
                yield return viewer1;
            }

            var viewer2 = ScrollingFiles?.GetScrollViewer();
            if (viewer2 != null)
            {
                yield return viewer2;
            }

            var viewer3 = ScrollingLinks?.GetScrollViewer();
            if (viewer3 != null)
            {
                yield return viewer3;
            }

            var viewer4 = ScrollingMusic?.GetScrollViewer();
            if (viewer4 != null)
            {
                yield return viewer4;
            }

            var viewer5 = ScrollingVoice?.GetScrollViewer();
            if (viewer5 != null)
            {
                yield return viewer5;
            }

            var viewer6 = _tab?.GetScrollViewer();
            if (viewer6 != null)
            {
                yield return viewer6;
            }
        }

        public event EventHandler<ScrollViewerViewChangedEventArgs> ViewChanged;

        private void InitializeSearch(TextBox field, Func<SearchMessagesFilter> filter)
        {
            var observable = Observable.FromEventPattern<TextChangedEventArgs>(field, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.Find(filter(), field.Text);
                field.Focus(FocusState.Programmatic);
            });
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedItems"))
            {
                var tab = _tab;
                var shift = 0;

                if (tab?.Index < 1)
                {
                    shift -= 1;
                }

                switch (ScrollingHost.SelectedIndex + shift)
                {
                    case 0:
                        ScrollingMedia.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 1:
                        ScrollingFiles.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 2:
                        ScrollingLinks.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 3:
                        ScrollingMusic.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 4:
                        ScrollingVoice.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                }
            }
        }

        public void OnBackRequested(HandledRoutedEventArgs args)
        {
            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.Tag as Message;

            var viewModel = new ChatGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, message.ChatId, 0, message, true);
            await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => element);
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            //ScrollingMedia.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingFiles.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingLinks.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingMusic.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;

            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                //InfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                //InfoPanel.Visibility = Visibility.Collapsed;
            }

            ViewModel.MessagesForwardCommand.RaiseCanExecuteChanged();
            ViewModel.MessagesDeleteCommand.RaiseCanExecuteChanged();
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ViewModel.SelectedItems = new List<Message>(((ListViewBase)sender).SelectedItems.Cast<Message>());
            }
        }

        private bool ConvertSelectionMode(ListViewSelectionMode mode)
        {
            List_SelectionModeChanged(null, null);
            return mode == ListViewSelectionMode.None ? false : true;
        }

        #region Context menu

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as Message;

            var selected = ViewModel.SelectedItems;
            if (selected.Count > 0)
            {
                if (selected.Contains(message))
                {
                    //TODO: New feature for later. ;) Needs Translation (Beta guys)
                    //flyout.CreateFlyoutItem(ViewModel.MessagesForwardCommand, "Forward Selected", new FontIcon { Glyph = Icons.Share });
                    //flyout.CreateFlyoutItem(ViewModel.MessagesDeleteCommand, "Delete Selected", new FontIcon { Glyph = Icons.Delete });
                    //flyout.CreateFlyoutItem(ViewModel.MessagesUnselectCommand, "Clear Selection");
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Additional.Select, new FontIcon { Glyph = Icons.Select });
                }
            }
            else
            {

                flyout.CreateFlyoutItem(MessageView_Loaded, ViewModel.MessageViewCommand, message, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Message });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Forward });
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Additional.Select, new FontIcon { Glyph = Icons.Select });
                flyout.CreateFlyoutItem(MessageSave_Loaded, ViewModel.MessageSaveCommand, message, Strings.Additional.SaveAs, new FontIcon { Glyph = Icons.SaveAs });
            }
            
            args.ShowAt(flyout, element);
        }

        private bool MessageView_Loaded(Message message)
        {
            return true;
        }

        private bool MessageSave_Loaded(Message message)
        {
            return true;
        }

        private bool MessageDelete_Loaded(Message message)
        {
            return message.CanBeDeletedOnlyForSelf || message.CanBeDeletedForAllUsers;
        }

        private bool MessageForward_Loaded(Message message)
        {
            return message.CanBeForwarded;
        }

        private bool MessageSelect_Loaded(Message message)
        {
            return ViewModel.SelectionMode == ListViewSelectionMode.None;
        }

        #endregion

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (sender is ListView)
                {
                    args.ItemContainer = new AccessibleChatListViewItem(ViewModel.ProtoService);
                }
                else
                {
                    args.ItemContainer = new ChatGridViewItem(ViewModel.ProtoService);
                }

                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Message_ContextRequested;
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var message = args.Item as Message;
            if (message == null)
            {
                return;
            }
            if (args.ItemContainer.ContentTemplateRoot is SimpleHyperlinkButton hyperlink)
            {
                if (message.Content is MessagePhoto photoMessage)
                {
                    var small = photoMessage.Photo.GetSmall();
                    var photo = hyperlink.Content as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 0, 0);
                }
                else if (message.Content is MessageVideo videoMessage && videoMessage.Video.Thumbnail != null)
                {
                    var grid = hyperlink.Content as Grid;
                    var photo = grid.Children[0] as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, videoMessage.Video.Thumbnail.File, 0, 0);

                    var panel = grid.Children[1] as Grid;
                    var duration = panel.Children[1] as TextBlock;
                    duration.Text = videoMessage.Video.GetDuration();
                }
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedFileCell fileCell)
            {
                fileCell.UpdateMessage(ViewModel.ProtoService, ViewModel, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedLinkCell linkCell)
            {
                linkCell.UpdateMessage(ViewModel.ProtoService, ViewModel.NavigationService, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedAudioCell audioCell)
            {
                audioCell.UpdateMessage(ViewModel.PlaybackService, ViewModel.ProtoService, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedVoiceCell voiceCell)
            {
                voiceCell.UpdateMessage(ViewModel.PlaybackService, ViewModel.ProtoService, message);
            }
            else if (message.Content is MessageHeaderDate && args.ItemContainer.ContentTemplateRoot is Border content && content.Child is TextBlock header)
            {
                header.Text = BindConvert.MonthGrouping(Utils.UnixTimestampToDateTime(message.Date));
            }

            if (args.ItemContainer.ContentTemplateRoot is FrameworkElement element)
            {
                element.Tag = message;
            }
        }

        public void UpdateFile(Telegram.Td.Api.File file)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.Media != null && viewModel.Media.TryGetMessagesForFileId(file.Id, out IList<Message> messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingMedia.ContainerFromItem(message) as GridViewItem;
                    var content = container?.ContentTemplateRoot as HyperlinkButton;

                    if (content == null)
                    {
                        continue;
                    }

                    if (message.Content is MessagePhoto photo)
                    {
                        var small = photo.Photo.GetSmall();
                        if (small != null && small.Photo.Id == file.Id && file.Local.IsDownloadingCompleted)
                        {
                            var thumbnail = content.Content as Image;
                            thumbnail.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 0, 0);
                        }
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        var thumb = video.Video.Thumbnail;
                        if (thumb != null && thumb.File.Id == file.Id && file.Local.IsDownloadingCompleted)
                        {
                            var grid = content.Content as Grid;
                            var thumbnail = grid.Children[0] as Image;
                            thumbnail.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, thumb.File, 0, 0);
                        }
                    }
                }

                if (file.Local.IsDownloadingCompleted && file.Remote.IsUploadingCompleted)
                {
                    messages.Clear();
                }
            }

            if (viewModel.Files != null && viewModel.Files.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingFiles.ContainerFromItem(message) as ListViewItem;
                    var content = container?.ContentTemplateRoot as SharedFileCell;

                    if (container == null)
                    {
                        continue;
                    }

                    content.UpdateFile(message, file);
                }
            }

            if (viewModel.Music != null && viewModel.Music.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingMusic.ContainerFromItem(message) as ListViewItem;
                    var content = container?.ContentTemplateRoot as SharedFileCell;

                    if (content == null)
                    {
                        continue;
                    }

                    content.UpdateFile(message, file);
                }
            }

            if (viewModel.Voice != null && viewModel.Voice.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingVoice.ContainerFromItem(message) as ListViewItem;
                    var content = container?.ContentTemplateRoot as SharedFileCell;

                    if (content == null)
                    {
                        continue;
                    }

                    content.UpdateFile(message, file);
                }
            }
        }

        private void Header_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatSharedMediaTab tab)
            {
                ScrollingHost.SelectedIndex = _tabs.IndexOf(tab);
            }
        }

        private void ScrollingHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Header.SelectedItem = _tabs[ScrollingHost.SelectedIndex];
            if (!Services.SettingsService.Current.Diagnostics.LoadMediaImmediately &&
                sender is FrameworkElement fe && fe.DataContext is ChatSharedMediaViewModel viewModel)
            { // Load all data, but media
                if (ViewModel.Files == null)
                    ViewModel.Find(new SearchMessagesFilterDocument(), null);
                if (ViewModel.Links == null)
                    ViewModel.Find(new SearchMessagesFilterUrl(), null);
                if (ViewModel.Music == null)
                    ViewModel.Find(new SearchMessagesFilterAudio(), null);
                if (ViewModel.Voice == null)
                    ViewModel.Find(new SearchMessagesFilterVoiceNote(), null);
            }
        }

        private void Scrolling_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isEmbedded)
            {
                return;
            }

            var selector = sender as ListViewBase;
            selector.ItemsPanelRoot.MinHeight = ScrollingHost.ActualHeight + 12;

            if (selector == ScrollingMedia)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingMedia_SizeChanged;
            }
            else if (selector == ScrollingFiles)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingFiles_SizeChanged;
            }
            else if (selector == ScrollingLinks)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingLinks_SizeChanged;
            }
            else if (selector == ScrollingMusic)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingMusic_SizeChanged;
            }
            else if (selector == ScrollingVoice)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingVoice_SizeChanged;
            }

            var scrollViewer = selector.GetScrollViewer();
            scrollViewer.ChangeView(null, 12, null, true);
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        private void Tab_Loaded(object sender, RoutedEventArgs e)
        {
            var tab = sender as IProfileTab;
            tab.Update(_isEmbedded, _isLocked);

            var selector = tab.GetSelector();
            selector.ItemsPanelRoot.MinHeight = ScrollingHost.ActualHeight + 12;

            selector.ItemsPanelRoot.SizeChanged += Tab_SizeChanged;

            var scrollViewer = selector.GetScrollViewer();
            scrollViewer.ChangeView(null, 12, null, true);
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ViewChanged?.Invoke(sender, e);
        }

        private void ScrollingHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isEmbedded)
            {
                return;
            }

            if (ScrollingMedia?.ItemsPanelRoot != null)
            {
                ScrollingMedia.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingMedia.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingFiles?.ItemsPanelRoot != null)
            {
                ScrollingFiles.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingFiles.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingLinks?.ItemsPanelRoot != null)
            {
                ScrollingLinks.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingLinks.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingMusic?.ItemsPanelRoot != null)
            {
                ScrollingMusic.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingMusic.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingVoice?.ItemsPanelRoot != null)
            {
                ScrollingVoice.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingVoice.GetScrollViewer().ChangeView(null, 12, null, true);
            }
            //TODO: Animations...
        }

        private void Tab_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_tab.GetScrollViewer().VerticalOffset < 12)
            {
                _tab.GetSelector().ItemsPanelRoot.SizeChanged -= Tab_SizeChanged;
                _tab.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingMedia_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingMedia.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingMedia.ItemsPanelRoot.SizeChanged -= ScrollingMedia_SizeChanged;
                ScrollingMedia.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingFiles_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingFiles.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingFiles.ItemsPanelRoot.SizeChanged -= ScrollingFiles_SizeChanged;
                ScrollingFiles.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingLinks_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingLinks.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingLinks.ItemsPanelRoot.SizeChanged -= ScrollingLinks_SizeChanged;
                ScrollingLinks.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingMusic_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingMusic.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingMusic.ItemsPanelRoot.SizeChanged -= ScrollingMusic_SizeChanged;
                ScrollingMusic.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingVoice_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingVoice.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingVoice.ItemsPanelRoot.SizeChanged -= ScrollingVoice_SizeChanged;
                ScrollingVoice.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Find(new SearchMessagesFilterPhotoAndVideo(), null);
            LoadMedia.Visibility = Visibility.Collapsed;
        }
    }

    public class ChatSharedMediaTab
    {
        public string Title { get; set; }

        public string Subtitle { get; set; }
    }

    public interface IProfileTab
    {
        int Index { get; }
        string Text { get; }

        ListViewBase GetSelector();
        ScrollViewer GetScrollViewer();

        void Update(bool embedded, bool locked);
    }
}
