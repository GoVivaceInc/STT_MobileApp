using Android.App;
using Android.Widget;
using Android.OS;
using System.Net.WebSockets;
using Android.Net;
using Android.Content;
using System.Threading.Tasks;
using System.Threading;
using System;
using Android.Media;
using Org.Json;

namespace Speech_Transcriptor
{
    [Activity(Label = "Speech_Transcriptor", MainLauncher = true, Icon = "@drawable/logogovivac")]
    public class MainActivity : Activity
    {
        Button startLlRecording = null;
        Button stopLlRecording = null;
        bool isLlRecording = false;
        ClientWebSocket websocket = null;
        private EditText ed;
        private TextView ShowStatus;
        private TextView ShowStatus1;
        private string comingData;
        private string finaldata="";
        private string errorNotifyer;
        private byte[] audioBuffer;
        private AudioRecord audioRecord;
        private bool stopped=false;
        private bool isPartialText = false;
        private string lastdata;
        string space = " ";
        void disableAllButtons()
        {
            startLlRecording.Enabled = false;
            stopLlRecording.Enabled = false;
        }

        void handleButtonState()
        {
            disableAllButtons();
            if (isLlRecording)
            {
                stopLlRecording.Enabled = true;
                return;
            }
            startLlRecording.Enabled = true;
        }
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
                // Set our view from the "main" layout resource
                SetContentView (Resource.Layout.Main);
                startLlRecording = FindViewById<Button>(Resource.Id.llStartRecordingButton);
                startLlRecording.Click += delegate
                {
                    startOperationAsync();
                    disableAllButtons();
                    isLlRecording = true;
                    handleButtonState();
                };

                stopLlRecording = FindViewById<Button>(Resource.Id.llEndRecordingButton);
                stopLlRecording.Click += delegate {
                    stopOperation();
                    isLlRecording = false;
                    handleButtonState();
                };
                handleButtonState();
        }

        private System.Boolean isNetworkAvailable()
        {
            ConnectivityManager connectivityManager
                  = (ConnectivityManager)GetSystemService(Context.ConnectivityService);
            NetworkInfo activeNetworkInfo = connectivityManager.ActiveNetworkInfo;//GetActiveNetworkInfo();
            return activeNetworkInfo != null && activeNetworkInfo.IsConnected;
        }

        async Task startOperationAsync()
        {
            try
            {
                if (!isNetworkAvailable())
                {
                    Toast.MakeText(this, " You currently have no network connection", ToastLength.Long).Show();
                    return;
                }
                websocket = new ClientWebSocket();
                if ((websocket.State == System.Net.WebSockets.WebSocketState.Open) | (websocket.State == System.Net.WebSockets.WebSocketState.Connecting))
                {
                    Toast.MakeText(this, " websocket is allready connecting", ToastLength.Long).Show();
                    return;
                }
                
                //string url = "ws://test.govivace.com:49169/client/ws/speech?content-type=audio/x-raw,+layout=(string)interleaved,+rate=(int)16000,+format=(string)S16LE,+channels=(int)1";                                                                                                                                                                                          //      Toast.MakeText(this, "websocket is initialized", ToastLength.Long).Show();
                //string url = "ws://test.govivace.com:49165/client/ws/speech";
                string url = "ws://mrcp.govivace.com:7682/english";
                await websocket.ConnectAsync(new System.Uri(url), CancellationToken.None);
                Toast.MakeText(this, "websocket is connected", ToastLength.Long).Show();
                stopped = false;
                ImageView imgView = FindViewById<ImageView>(Resource.Id.imageview);
                imgView.Visibility= Android.Views.ViewStates.Visible;
                ShowStatus1 = FindViewById<TextView>(Resource.Id.status1);
                ShowStatus1.Text = "REC";
                ShowStatus = FindViewById<TextView>(Resource.Id.status);
                ShowStatus.Text = "";
                await Task.WhenAll(Send(websocket),Recieve(websocket));

            }
            catch (Java.Lang.Exception ex)
            {
                System.Console.WriteLine("exception{0}", ex);
            }
            finally
            {
                if (websocket != null)
                {
                    websocket.Dispose();
                    websocket = null;
                    Console.WriteLine("websocket closed");
                    isLlRecording = false;
                    handleButtonState();
                    ShowStatus = FindViewById<TextView>(Resource.Id.status);
                    ShowStatus.Text = "Complete";
                }
            }

        }

        private  async Task Send(ClientWebSocket websocket)
        {
            await StartRecorderAsync(websocket);
        }

        

        protected async Task StartRecorderAsync(ClientWebSocket websocket)
        {
            int readBytes = 0;
            int countBytes = AudioRecord.GetMinBufferSize(8000, ChannelIn.Mono, Android.Media.Encoding.Pcm16bit);
            audioBuffer = new byte[100000];
            if (countBytes > 0)
            {
                audioRecord = new AudioRecord(
                    // Hardware source of recording.
                    AudioSource.Mic,
                    // Frequency
                    8000,
                    // Mono or stereo
                    ChannelIn.Mono,
                    // Audio encoding
                    Android.Media.Encoding.Pcm16bit,
                    // Length of the audio clip.
                    audioBuffer.Length
                );
                Console.WriteLine("++++++++Audio recording start++++++++");
                audioRecord.StartRecording();
                while (!stopped)
                {
                    readBytes = await audioRecord.ReadAsync(audioBuffer, 0, audioBuffer.Length);

                    if (websocket != null && !stopped)
                    {
                        Console.WriteLine("++++++++call send method++++++++++");
                        await websocket.SendAsync(new ArraySegment<byte>(audioBuffer, 0, readBytes), WebSocketMessageType.Binary, false, CancellationToken.None);
                        //await Task.Delay(250);
                         Array.Clear(audioBuffer, 0, audioBuffer.Length);
                    }
                }
            }
        }

        void stopOperation()
        {
            Stop();
        }

        public async void Stop()
        {
            if (!stopped)
            {
                Console.WriteLine("+++++++++++++send EOS+++++++++++++");
                string eos = "EOS"; // for end of stream
                byte[] eosData = System.Text.Encoding.ASCII.GetBytes(eos);
                await websocket.SendAsync(new ArraySegment<byte>(eosData, 0, eosData.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
                stopped = true;
                ImageView imgView = FindViewById<ImageView>(Resource.Id.imageview);
                imgView.Visibility = Android.Views.ViewStates.Invisible;
                ShowStatus1 = FindViewById<TextView>(Resource.Id.status1);
                ShowStatus1.Text = "";
            }
            stopRecording();
        }

        private void stopRecording()
        {
            if (audioRecord != null)
            {   
                audioRecord.Stop();
                audioRecord.Release();
                audioRecord = null;
                //websocket = null;
            }
        }
        private  async Task Recieve(ClientWebSocket websocket)
        {
            var buffer = new byte[100000];
            
            
            try
            {
                while (websocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    else
                    {
                        ShowStatus = FindViewById<TextView>(Resource.Id.status);
                        ShowStatus.Text = "recieving text..";
                        string data = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine("transcript json"+data);
                        JSONObject parentObject = new JSONObject(data);
                        int status = parentObject.OptInt("status");
                        if (status == 1)
                        {
                            errorNotifyer = "Speech contains a large portion of silence or non-speech";
                            RunOnUiThread(writeErrorOnUI);
                        }
                        else if (status == 9)
                        {
                            errorNotifyer = parentObject.OptString("message");
                            RunOnUiThread(writeErrorOnUI);
                        }
                        else if (status == 5)
                        {
                            errorNotifyer = parentObject.OptString("message");
                            RunOnUiThread(writeErrorOnUI);
                        }
                        if (status == 0)
                        {
                            JSONObject hypotheses = parentObject.OptJSONObject("result");
                            bool final = hypotheses.GetBoolean("final");
                            JSONArray jsonArray = hypotheses.OptJSONArray("hypotheses");
                            for (int i = 0; i < jsonArray.Length(); i++)
                            {
                                JSONObject jsonObject = jsonArray.GetJSONObject(i); // getting JSON Object at I'th index
                                string name = jsonObject.OptString("transcript");
                                Console.WriteLine("transcript:  " + name);
                                comingData = name;
                                
                                if(final)
                                {
                                    if(isPartialText)
                                    {
                                        ed = FindViewById<EditText>(Resource.Id.editText1);
                                        int place = ed.Text.LastIndexOf(lastdata);
                                        ed.Text=ed.Text.Remove(place, lastdata.Length);
                                    }
                                    isPartialText = false;
                                    //finaldata = finaldata + name;
                                    //RunOnUiThread(writeDataOnUIFinal);
                                    RunOnUiThread(appenddata);
                                    lastdata = comingData;
                                    finaldata = ed.Text;
                                }
                                else
                                {
                                    if (isPartialText)
                                    {
                                      int place = ed.Text.LastIndexOf(lastdata);
                                      ed.Text =  ed.Text.Remove(place, lastdata.Length);
                                    }
                                    //RunOnUiThread(writeDataOnUI);
                                    isPartialText = true;
                                    RunOnUiThread(appenddata);
                                    lastdata = comingData;
                                   

                                }
                                
                            }
                             
                        }
                        Array.Clear(buffer, 0, buffer.Length);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                errorNotifyer = ex.Message.ToString();
            }
        }

        private void writeDataOnUI()
        {
            ed = FindViewById<EditText>(Resource.Id.editText1);
            ed.Text = finaldata+comingData;
        }
        private void appenddata()
        {
            ed = FindViewById<EditText>(Resource.Id.editText1);
            ed.Append(comingData);
        }
        private void writeDataOnUIFinal()
        {
            ed = FindViewById<EditText>(Resource.Id.editText1);
            ed.Text=finaldata;
        }

        private void writeErrorOnUI()
        {
            Toast.MakeText(this, errorNotifyer, ToastLength.Long).Show();
        }
    }
}

