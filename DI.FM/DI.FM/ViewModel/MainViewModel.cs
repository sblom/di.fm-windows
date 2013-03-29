using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace DI.FM.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        #region Variables

        private DispatcherTimer nowPlayingRefresh;

        #endregion

        #region Properties

        private ObservableCollection<ChannelItem> _allChannels;
        public ObservableCollection<ChannelItem> AllChannels
        {
            get { return _allChannels; }
            set
            {
                _allChannels = value;
                RaisePropertyChanged("AllChannels");
            }
        }

        private ObservableCollection<ChannelItem> _favoriteChannels;
        public ObservableCollection<ChannelItem> FavoriteChannels
        {
            get { return _favoriteChannels; }
            set
            {
                _favoriteChannels = value;
                RaisePropertyChanged("FavoriteChannels");
            }
        }

        public IEnumerable<ChannelItem> MainFavoriteChannels
        {
            get { return this.FavoriteChannels.Take(6).ToList(); }
        }

        private ChannelItem _nowPlayingItem;
        public ChannelItem NowPlayingItem
        {
            get { return _nowPlayingItem; }
            set
            {
                _nowPlayingItem = value;

                if (_nowPlayingItem != null)
                {
                    NowPlayingRefresh_Tick(null, null);
                    nowPlayingRefresh.Start();
                }
                else
                {
                    nowPlayingRefresh.Stop();
                }

                RaisePropertyChanged("NowPlayingItem");
            }
        }

        #endregion

        #region Constructor

        public MainViewModel()
        {
            if (!IsInDesignMode)
            {
                // Init the arrays
                //AllChannels = new ObservableCollection<ChannelItem>();
                FavoriteChannels = new ObservableCollection<ChannelItem>();
                FavoriteChannels.CollectionChanged += (sender, e) => { RaisePropertyChanged("MainFavoriteChannels"); };
                // Init the timer
                nowPlayingRefresh = new DispatcherTimer();
                nowPlayingRefresh.Interval = TimeSpan.FromSeconds(1);
                nowPlayingRefresh.Tick += NowPlayingRefresh_Tick;
                // Load the channels
                //LoadAllChannels();

                //IsPremium();

                GetIsPremium();
                CreateEmptyChannels();
            }
        }


        #endregion

        #region Load + Save

        public async Task LoadAllChannels(bool forceDownload = false)
        {
            CreateEmptyChannels();

            /*AllChannels.Clear();
            FavoriteChannels.Clear();

            StorageFile file = null;
            try { file = await ApplicationData.Current.LocalFolder.GetFileAsync("channels.json"); }
            catch { }

            string data = null;

            if (file == null || forceDownload)
            {
                data = await ChannelsHelper.DownloadJson(ChannelsHelper.CHANNELS_URL);
                if (data != null)
                {
                    file = await ApplicationData.Current.LocalFolder.CreateFileAsync("channels.json", CreationCollisionOption.ReplaceExisting);
                    var writer = new StreamWriter(await file.OpenStreamForWriteAsync());
                    await writer.WriteAsync(data);
                    writer.Dispose();
                }
            }
            else
            {
                var reader = new StreamReader(await file.OpenStreamForReadAsync());
                data = await reader.ReadToEndAsync();
                reader.Dispose();
            }

            if (data == null) return;

            var channels = JsonConvert.DeserializeObject(data) as JContainer;

            foreach (var asset in ChannelsHelper.ChannelsAssets)
            {
                var item = new ChannelItem();
                item.Key = asset.Key;
                item.Image = asset.Value[0];
                item.Color1 = asset.Value[1];
                item.Color2 = asset.Value[2];

                var channel = channels.FirstOrDefault((element) => element["key"].Value<string>() == asset.Key);

                if (channel != null)
                {
                    item.ID = channel.Value<int>("id");
                    item.Name = channel.Value<string>("name");
                    item.Description = channel.Value<string>("description");
                }

                LoadChannelStreams(item);
                LoadTrackHistory(item);

                AllChannels.Add(item);
            }

            if (AllChannels.Count > 2)
            {
                AllChannels[0].Next = AllChannels[1];
                AllChannels[AllChannels.Count - 1].Prev = AllChannels[AllChannels.Count - 2];
            }

            for (int i = 1; i < AllChannels.Count - 1; i++)
            {
                AllChannels[i].Prev = AllChannels[i - 1];
                AllChannels[i].Next = AllChannels[i + 1];
            }

            await LoadFavoriteChannels();*/
        }

        private async void LoadChannelStreams(ChannelItem channel)
        {
            channel.Streams = new List<string>();
            var data = await ChannelsHelper.DownloadJson(ChannelsHelper.CHANNELS_URL + "/" + channel.Key);

            if (data == null) return;

            var streams = JsonConvert.DeserializeObject(data) as JContainer;

            foreach (var stream in streams)
            {
                channel.Streams.Add(stream.ToObject<string>());
            }
        }

        private async void LoadTrackHistory(ChannelItem channel)
        {
            var data = await ChannelsHelper.DownloadJson(string.Format(ChannelsHelper.TRACK_URL, channel.ID));
            if (data == null) return;

            var tempTracks = new List<TrackItem>();

            var index = 0;
            var tracks = JsonConvert.DeserializeObject(data) as JContainer;

            foreach (var track in tracks)
            {
                if (track["type"].Value<string>() == "track")
                {
                    tempTracks.Add(new TrackItem()
                    {
                        Index = index + 1,
                        Track = track.Value<string>("track"),
                        Started = track.Value<long>("started"),
                        Duration = track.Value<int>("duration")
                    });

                    if (index == 4) break;
                    index++;
                }
            }

            if (channel.TrackHistory != null && channel.TrackHistory.Count > 0 && tempTracks.Count > 0 && channel.TrackHistory[0].Started == tempTracks[0].Started)
            {
                channel.TrackHistory[0].Started = -1;
                return;
            }

            if (tempTracks.Count > 0)
            {
                channel.NowPlaying = tempTracks[0];
                channel.TrackHistory = new ObservableCollection<TrackItem>(tempTracks);
            }
        }

        private async Task LoadFavoriteChannels()
        {
            StorageFile file = null;

            try { file = await ApplicationData.Current.LocalFolder.GetFileAsync("favorites.txt"); }
            catch { }

            if (file != null)
            {
                using (var reader = new StreamReader(await file.OpenStreamForReadAsync()))
                {
                    var favorites = await reader.ReadToEndAsync();
                    var array = favorites.Split(';');

                    foreach (var channel in AllChannels)
                    {
                        if (array.Contains(channel.Key))
                        {
                            FavoriteChannels.Add(channel);
                        }
                    }
                }
            }
        }

        public async Task SaveFavoriteChannels()
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("favorites.txt", CreationCollisionOption.ReplaceExisting);

            using (var writer = new StreamWriter(await file.OpenStreamForWriteAsync()))
            {
                foreach (var channel in FavoriteChannels)
                {
                    await writer.WriteAsync(channel.Key + ";");
                }

                await writer.FlushAsync();
            }
        }

        #endregion

        private void NowPlayingRefresh_Tick(object sender, object e)
        {
            if (NowPlayingItem.NowPlaying == null || NowPlayingItem.NowPlaying.Started == -1)
            {
                // Reload one more time if last reload was not successful
                LoadTrackHistory(NowPlayingItem);
                nowPlayingRefresh.Stop();
                return;
            }

            var currentPosition = NowPlayingItem.NowPlaying.StartedTime;
            if (currentPosition > NowPlayingItem.NowPlaying.Duration)
            {
                // Reload now playing if music ended and set position to maximum
                NowPlayingItem.NowPlaying.Position = NowPlayingItem.NowPlaying.Duration;
                LoadTrackHistory(NowPlayingItem);
            }
            else
            {
                NowPlayingItem.NowPlaying.Position = (int)currentPosition;
            }
        }





        private MediaElement _mediaPlayer;
        public MediaElement MediaPlayer
        {
            get { return _mediaPlayer; }
            set
            {
                _mediaPlayer = value;
                _mediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
                _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            }
        }

        private async void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (NowPlayingItem != null)
            {
                StreamIndex++;

                if (StreamIndex < NowPlayingItem.Streams.Count)
                {
                    MediaPlayer.Source = new Uri(NowPlayingItem.Streams[StreamIndex]);
                }
                else
                {
                    var msg = new MessageDialog("Could not connect on any of available streams!", "Cannot play channel");
                    await msg.ShowAsync();
                }
            }
        }

        private void MediaPlayer_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            IsPlaying = MediaPlayer.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Playing;
            IsBuffering = MediaPlayer.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Buffering || MediaPlayer.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Opening;

        }

        private int _streamIndex;
        public int StreamIndex
        {
            get { return _streamIndex; }
            set
            {
                _streamIndex = value;
                RaisePropertyChanged("StreamIndex");
            }
        }

        private bool _isBuffering;
        public bool IsBuffering
        {
            get { return _isBuffering; }
            set
            {
                if (_isBuffering != value)
                {
                    _isBuffering = value;
                    RaisePropertyChanged("IsBuffering");
                }
            }
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    RaisePropertyChanged("IsPlaying");
                }
            }
        }

        public void PlayChannel(ChannelItem channel)
        {
            if (channel.Streams.Count > 0)
            {
                StreamIndex = 0;
                NowPlayingItem = channel;
                MediaPlayer.Source = new Uri(channel.Streams[0]);                
            }
        }

        public void TogglePlay()
        {
            if (MediaPlayer.CurrentState == Windows.UI.Xaml.Media.MediaElementState.Playing)
            {
                MediaPlayer.Source = null;
            }
            else
            {
                PlayChannel(NowPlayingItem);
            }
        }




        private async void CreateEmptyChannels()
        {
            AllChannels = new ObservableCollection<ChannelItem>();

            int i = 0;

            foreach (var item in ChannelsHelper.ChannelsAssets)
            {
                var chn = new ChannelItem()
                {
                    Key = item.Key,
                    Image = item.Value[0],
                    Color1 = item.Value[1],
                    Color2 = item.Value[2],
                    Streams = new List<string>()
                };

                if (i > 0)
                {
                    chn.Prev = AllChannels[AllChannels.Count - 1];
                    AllChannels[AllChannels.Count - 1].Next = chn;
                }

                AllChannels.Add(chn);

                i++;
            }

            UpdateChannels();
        }

        public async void UpdateChannels()
        {
            var url = "http://api.audioaddict.com/v1/di/mobile/batch_update?stream_set_key=public3,premium_high";
            string data = null;

            var cli = new HttpClient();
            cli.DefaultRequestHeaders.Authorization = CreateBasicHeader("ephemeron", "dayeiph0ne@pp");
            data = await cli.GetStringAsync(url);

            if (data != null)
            {
                var batch = JsonConvert.DeserializeObject(data) as JContainer;
                GetChannelsInfo(batch["channel_filters"].First["channels"]);
                GetChannelsStreams(batch["streamlists"]);
                GetChannelsNowPlaying(batch["track_history"]);
            }
        }

        private void GetChannelsInfo(JToken token)
        {
            foreach (var item in AllChannels)
            {
                var chn = token.FirstOrDefault((e) => e.Value<string>("key") == item.Key);
                if (chn != null)
                {
                    item.ID = chn.Value<int>("id");
                    item.Name = chn.Value<string>("name");
                    item.Description = chn.Value<string>("description");
                }
            }
        }

        public void GetChannelsStreams(JToken token)
        {
            JToken chn = null;
            if (IsPremium) chn = token["premium_high"];
            else chn = token["public3"];

            if (chn != null)
            {
                var strms = chn["channels"];

                foreach (var item in AllChannels)
                {
                    var urls = strms.FirstOrDefault((e) => e.Value<int>("id") == item.ID);

                    item.Streams.Clear();
                    var li = urls["streams"];

                    if (li == null) continue;

                    foreach (var urll in li)
                    {
                        if (IsPremium)
                        {
                            item.Streams.Add(urll.Value<string>("url") + "?" + ListenKey);
                        }
                        else
                        {
                            item.Streams.Add(urll.Value<string>("url"));
                        }
                    }
                }
            }
        }


        private void GetChannelsNowPlaying(JToken token)
        {
            foreach (var item in AllChannels)
            {
                var chn = token.FirstOrDefault((e) =>
                {
                    var x = e.First.Value<int>("channel_id");
                    return x == item.ID;
                }).First;

                if (chn != null)
                {
                    
                    item.NowPlaying = new TrackItem()
                    {
                        Track = chn.Value<string>("track"),
                        Started = chn.Value<int>("started"),
                        Duration = chn.Value<int>("duration")
                    };
                }
            }
        }

        public async void GetChannelsStresms()
        {
            var url = "http://api.audioaddict.com/v1/di/mobile/batch_update?stream_set_key=public3,premium_high";
            string data = null;

            var cli = new HttpClient();
            cli.DefaultRequestHeaders.Authorization = CreateBasicHeader("ephemeron", "dayeiph0ne@pp");
            data = await cli.GetStringAsync(url);

            if (data != null)
            {
                var batch = JsonConvert.DeserializeObject(data) as JContainer;
                var listsd = batch["streamlists"];

                JToken chn = null;
                if (IsPremium) chn = listsd["premium_high"];
                else chn = listsd["public3"];

                if (chn != null)
                {
                    var strms = chn["channels"];

                    foreach (var item in AllChannels)
                    {
                        var urls = strms.FirstOrDefault((e) => e.Value<int>("id") == item.ID);

                        item.Streams.Clear();
                        var li = urls["streams"];

                        foreach (var urll in li)
                        {
                            if (IsPremium)
                            {
                                item.Streams.Add(urll.Value<string>("url") + "?" + ListenKey);
                            }
                            else
                            {
                                item.Streams.Add(urll.Value<string>("url"));
                            }
                        }
                    }
                }
            }
        }







        /*public void ReUpdateChannelStreams()
        {
            foreach (var item in AllChannels)
            {
                GetChannelStream(item, IsPremium);
            }
        }

        private async void GetChannelStream(ChannelItem channel, bool premium)
        {
            await Task.Factory.StartNew(async () =>
            {
                string data = null;
                if (premium) data = await ChannelsHelper.DownloadJson(ChannelsHelper.PREMIUM_CHANNELS_URL + @"\" + channel.Key + "?" + ListenKey);
                else data = await ChannelsHelper.DownloadJson(ChannelsHelper.FREE_CHANNELS_URL + @"\" + channel.Key);
                if (data != null)
                {
                    var streams = JsonConvert.DeserializeObject(data) as JContainer;

                    channel.Streams.Clear();
                    foreach (var stream in streams)
                    {
                        channel.Streams.Add(stream.Value<string>());
                    }
                }
            });
        }*/

        public AuthenticationHeaderValue CreateBasicHeader(string username, string password)
        {
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(username + ":" + password);
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        


        public string ListenKey
        {
            get
            {
                return ApplicationData.Current.LocalSettings.Values["ListenKey"] as string;
                //return "31c818d91fe2eae4814bbc2f";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["ListenKey"] = value;
            }
        }


        private string[] streamUrls = new string[] { "http://prem2.di.fm:80/deeptech", "http://prem1.di.fm:80/deeptech", "http://prem4.di.fm:80/deeptech", "http://prem2.di.fm:80/darkdnb", "http://prem4.di.fm:80/darkdnb", "http://prem1.di.fm:80/darkdnb", "http://prem2.di.fm:80/liquiddubstep", "http://prem1.di.fm:80/liquiddubstep", "http://prem4.di.fm:80/liquiddubstep", "http://prem2.di.fm:80/glitchhop", "http://prem1.di.fm:80/glitchhop", "http://prem4.di.fm:80/glitchhop", "http://prem2.di.fm:80/trance", "http://prem1.di.fm:80/trance", "http://prem4.di.fm:80/trance", "http://prem2.di.fm:80/classiceurodisco", "http://prem1.di.fm:80/classiceurodisco", "http://prem4.di.fm:80/classiceurodisco", "http://prem2.di.fm:80/vocaltrance", "http://prem1.di.fm:80/vocaltrance", "http://prem4.di.fm:80/vocaltrance", "http://prem2.di.fm:80/chillout", "http://prem1.di.fm:80/chillout", "http://prem4.di.fm:80/chillout", "http://prem2.di.fm:80/progressive", "http://prem1.di.fm:80/progressive", "http://prem4.di.fm:80/progressive", "http://prem2.di.fm:80/lounge", "http://prem1.di.fm:80/lounge", "http://prem4.di.fm:80/lounge", "http://prem2.di.fm:80/house", "http://prem1.di.fm:80/house", "http://prem4.di.fm:80/house", "http://prem2.di.fm:80/vocalchillout", "http://prem1.di.fm:80/vocalchillout", "http://prem4.di.fm:80/vocalchillout", "http://prem2.di.fm:80/minimal", "http://prem1.di.fm:80/minimal", "http://prem4.di.fm:80/minimal", "http://prem2.di.fm:80/harddance", "http://prem1.di.fm:80/harddance", "http://prem4.di.fm:80/harddance", "http://prem2.di.fm:80/electrohouse", "http://prem1.di.fm:80/electrohouse", "http://prem4.di.fm:80/electrohouse", "http://prem2.di.fm:80/eurodance", "http://prem1.di.fm:80/eurodance", "http://prem4.di.fm:80/eurodance", "http://prem2.di.fm:80/techhouse", "http://prem1.di.fm:80/techhouse", "http://prem4.di.fm:80/techhouse", "http://prem2.di.fm:80/psychill", "http://prem1.di.fm:80/psychill", "http://prem4.di.fm:80/psychill", "http://prem2.di.fm:80/goapsy", "http://prem1.di.fm:80/goapsy", "http://prem4.di.fm:80/goapsy", "http://prem2.di.fm:80/progressivepsy", "http://prem4.di.fm:80/progressivepsy", "http://prem1.di.fm:80/progressivepsy", "http://prem2.di.fm:80/hardcore", "http://prem1.di.fm:80/hardcore", "http://prem4.di.fm:80/hardcore", "http://prem2.di.fm:80/djmixes", "http://prem1.di.fm:80/djmixes", "http://prem4.di.fm:80/djmixes", "http://prem2.di.fm:80/ambient", "http://prem1.di.fm:80/ambient", "http://prem4.di.fm:80/ambient", "http://prem2.di.fm:80/drumandbass", "http://prem1.di.fm:80/drumandbass", "http://prem4.di.fm:80/drumandbass", "http://prem2.di.fm:80/classicelectronica", "http://prem1.di.fm:80/classicelectronica", "http://prem4.di.fm:80/classicelectronica", "http://prem2.di.fm:80/epictrance", "http://prem4.di.fm:80/epictrance", "http://prem1.di.fm:80/epictrance", "http://prem2.di.fm:80/ukgarage", "http://prem4.di.fm:80/ukgarage", "http://prem1.di.fm:80/ukgarage", "http://prem2.di.fm:80/cosmicdowntempo", "http://prem1.di.fm:80/cosmicdowntempo", "http://prem4.di.fm:80/cosmicdowntempo", "http://prem2.di.fm:80/breaks", "http://prem1.di.fm:80/breaks", "http://prem4.di.fm:80/breaks", "http://prem2.di.fm:80/techno", "http://prem1.di.fm:80/techno", "http://prem4.di.fm:80/techno", "http://prem2.di.fm:80/soulfulhouse", "http://prem1.di.fm:80/soulfulhouse", "http://prem4.di.fm:80/soulfulhouse", "http://prem2.di.fm:80/deephouse", "http://prem4.di.fm:80/deephouse", "http://prem1.di.fm:80/deephouse", "http://prem2.di.fm:80/tribalhouse", "http://prem1.di.fm:80/tribalhouse", "http://prem4.di.fm:80/tribalhouse", "http://prem2.di.fm:80/funkyhouse", "http://prem1.di.fm:80/funkyhouse", "http://prem4.di.fm:80/funkyhouse", "http://prem2.di.fm:80/deepnudisco", "http://prem1.di.fm:80/deepnudisco", "http://prem4.di.fm:80/deepnudisco", "http://prem2.di.fm:80/spacemusic", "http://prem1.di.fm:80/spacemusic", "http://prem4.di.fm:80/spacemusic", "http://prem2.di.fm:80/hardstyle", "http://prem1.di.fm:80/hardstyle", "http://prem4.di.fm:80/hardstyle", "http://prem2.di.fm:80/chilloutdreams", "http://prem1.di.fm:80/chilloutdreams", "http://prem4.di.fm:80/chilloutdreams", "http://prem2.di.fm:80/liquiddnb", "http://prem1.di.fm:80/liquiddnb", "http://prem4.di.fm:80/liquiddnb", "http://prem2.di.fm:80/classiceurodance", "http://prem1.di.fm:80/classiceurodance", "http://prem4.di.fm:80/classiceurodance", "http://prem2.di.fm:80/handsup", "http://prem1.di.fm:80/handsup", "http://prem4.di.fm:80/handsup", "http://prem2.di.fm:80/club", "http://prem1.di.fm:80/club", "http://prem4.di.fm:80/club", "http://prem2.di.fm:80/classictrance", "http://prem1.di.fm:80/classictrance", "http://prem4.di.fm:80/classictrance", "http://prem2.di.fm:80/classicvocaltrance", "http://prem1.di.fm:80/classicvocaltrance", "http://prem4.di.fm:80/classicvocaltrance", "http://prem2.di.fm:80/dubstep", "http://prem1.di.fm:80/dubstep", "http://prem4.di.fm:80/dubstep", "http://prem2.di.fm:80/clubdubstep", "http://prem4.di.fm:80/clubdubstep", "http://prem1.di.fm:80/clubdubstep", "http://prem2.di.fm:80/discohouse", "http://prem1.di.fm:80/discohouse", "http://prem4.di.fm:80/discohouse", "http://prem2.di.fm:80/futuresynthpop", "http://prem1.di.fm:80/futuresynthpop", "http://prem4.di.fm:80/futuresynthpop", "http://prem2.di.fm:80/latinhouse", "http://prem1.di.fm:80/latinhouse", "http://prem4.di.fm:80/latinhouse", "http://prem2.di.fm:80/oldschoolacid", "http://prem1.di.fm:80/oldschoolacid", "http://prem4.di.fm:80/oldschoolacid", "http://prem2.di.fm:80/chiptunes", "http://prem1.di.fm:80/chiptunes", "http://prem4.di.fm:80/chiptunes" };

        public async void GetIsPremium()
        {
            var key = ListenKey;
            if (key == null)
            {
                IsPremium = false;
                return;
            }

            var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };

            int maxR = 3;
            int index = 0;

            var rand = new Random();
            
            while(index < maxR)
            {
                var url = streamUrls[rand.Next(0, streamUrls.Length - 1)] + "?" + key;

                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    var response = await request.GetResponseAsync();
                    IsPremium = true;
                    break;
                }
                catch(Exception ex)
                {
                    IsPremium = false;
                    index++;
                }
            }
        }

        private bool _isPremium;
        public bool IsPremium
        {
            get { return _isPremium; }
            set
            {
                _isPremium = value;
                RaisePropertyChanged("IsPremium");
            }
        }
    }
}
