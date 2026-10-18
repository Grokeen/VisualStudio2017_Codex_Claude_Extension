using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownAnimatedImage
    /// desc         : 허용된 로컬 안내 이미지와 유한한 GIF 프레임만 읽고 표시 수명 동안 애니메이션한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 HTML·외부 URI·공유 브라우저 없이 PNG/JPEG/GIF 지원.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed class MarkdownAnimatedImage : Image, IDisposable
    {
        public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register("SourcePath",typeof(string),typeof(MarkdownAnimatedImage),new PropertyMetadata(null,AssetChanged));
        public static readonly DependencyProperty AssetBaseDirectoryProperty = DependencyProperty.Register("AssetBaseDirectory",typeof(string),typeof(MarkdownAnimatedImage),new PropertyMetadata(null,AssetChanged));
        public string SourcePath { get { return (string)GetValue(SourcePathProperty); } set { SetValue(SourcePathProperty,value); } }
        public string AssetBaseDirectory { get { return (string)GetValue(AssetBaseDirectoryProperty); } set { SetValue(AssetBaseDirectoryProperty,value); } }
        public int FrameCount { get { return frames == null ? 0 : frames.Images.Count; } }
        public long DecodedPixelCount { get { return frames == null ? 0 : frames.Pixels; } }
        public bool IsAnimationRunning { get { return timer.IsEnabled; } }
        public string FailureReason { get; private set; }
        private MarkdownImageFrames frames;
        private readonly DispatcherTimer timer;
        private int frameIndex;
        private bool disposed;

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownAnimatedImage
        /// desc         : Loaded·Visible일 때만 한 타이머를 사용하고 화면에서 제거되면 정지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 생성·숨김·Unloaded에서 무제한 백그라운드 타이머를 만들지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public MarkdownAnimatedImage()
        {
            Stretch=Stretch.Uniform;MaxWidth=900;MaxHeight=520;
            timer=new DispatcherTimer(DispatcherPriority.Background,Dispatcher);timer.Tick+=Advance;
            Loaded+=VisibilityUpdated;Unloaded+=VisibilityUpdated;IsVisibleChanged+=VisibleChanged;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssetChanged
        /// desc         : 입력 경로가 바뀌면 이전 프레임을 해제한 뒤 같은 제한으로 다시 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : base가 없으면 디스크·네트워크 읽기 없이 Source를 비움.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void AssetChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args) { ((MarkdownAnimatedImage)sender).Reload(); }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Reload
        /// desc         : 검증된 바이트만 WIC에 전달하고 실패는 실행 없는 이미지 대체 상태로 남긴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 공개되지 않은 파일 경로나 원문 decoder 오류를 표시하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Reload()
        {
            if(timer==null||disposed)return;timer.Stop();frames=null;Source=null;frameIndex=0;FailureReason=null;
            if(String.IsNullOrWhiteSpace(SourcePath)||String.IsNullOrWhiteSpace(AssetBaseDirectory)){FailureReason="이미지는 명시한 로컬 안내 폴더에서만 표시합니다.";return;}
            try { frames=MarkdownImageFrames.Load(MarkdownAssetReader.Read(AssetBaseDirectory,SourcePath));Source=frames.Images[0];UpdateAnimation(); }
            catch(Exception error) when (error is IOException||error is InvalidDataException||error is FormatException||error is COMException||error is ArgumentException||error is InvalidOperationException||error is NotSupportedException||error is System.Security.SecurityException||error is OverflowException)
            { frames=null;Source=null;FailureReason="이미지를 읽지 못했거나 허용 범위·크기·프레임 제한을 넘었습니다."; }
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisibilityUpdated
        /// desc         : 실제 WPF 표시 수명에 맞춰 애니메이션만 시작·정지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 닫힌 도움말의 GIF가 계속 실행되지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void VisibilityUpdated(object sender,RoutedEventArgs args){UpdateAnimation();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : VisibleChanged
        /// desc         : 접힌 탭·구역에서는 타이머를 정지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다시 보일 때 기존 프레임으로 재개.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void VisibleChanged(object sender,DependencyPropertyChangedEventArgs args){UpdateAnimation();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateAnimation
        /// desc         : 프레임 수·표시 여부와 OS 애니메이션 설정에 따라 단일 clock을 제어한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 모션을 끈 사용자에게는 첫 정지 화면을 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateAnimation(){if(!disposed&&frames!=null&&frames.Images.Count>1&&IsLoaded&&IsVisible&&SystemParameters.ClientAreaAnimation){timer.Interval=TimeSpan.FromMilliseconds(frames.Delays[frameIndex]);timer.Start();}else timer.Stop();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Advance
        /// desc         : 검증·합성이 끝난 frozen 프레임만 실제 지연 시간으로 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 매 tick 파일 읽기·압축 해제·네트워크 요청 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void Advance(object sender,EventArgs args){if(frames==null||disposed){timer.Stop();return;}frameIndex=(frameIndex+1)%frames.Images.Count;Source=frames.Images[frameIndex];timer.Interval=TimeSpan.FromMilliseconds(frames.Delays[frameIndex]);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 타이머·이벤트·프레임을 해제하고 같은 객체를 다시 읽지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 문서 교체에서 이미지 메모리가 누적되지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Tick-=Advance;Loaded-=VisibilityUpdated;Unloaded-=VisibilityUpdated;IsVisibleChanged-=VisibleChanged;Source=null;frames=null;}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownAssetReader
    /// desc         : 지정한 root 내부의 실제 최종 파일 핸들만 제한된 바이트로 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : UNC·장치·reparse·원격 URI·root 탈출·읽기 중 성장 거부.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal static class MarkdownAssetReader
    {
        internal const int MaximumBytes=2*1024*1024;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint length,uint flags);
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Read
        /// desc         : 네트워크를 사용하지 않고 열린 핸들의 실제 경로와 모든 기존 상위 폴더를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 검사 후 경로 교체로 root 밖 이미지를 읽는 경쟁을 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static byte[] Read(string root,string relative,int maximumBytes=MaximumBytes,bool allowEmpty=false,bool decodeUri=true)
        {
            if(String.IsNullOrWhiteSpace(root)||String.IsNullOrWhiteSpace(relative)||relative.Length>2048||relative.Any(Char.IsControl))throw new InvalidOperationException("이미지 폴더가 필요합니다.");
            if(maximumBytes<1||maximumBytes>MaximumBytes)throw new ArgumentOutOfRangeException("maximumBytes");RequireDrive(root);root=Path.GetFullPath(root);if(root.Length>3)root=root.TrimEnd('\\','/');RequireDrive(root);RejectReparse(root);
            if(decodeUri)relative=Uri.UnescapeDataString(relative);
            if(relative.StartsWith("\\\\",StringComparison.Ordinal)||relative.StartsWith("//",StringComparison.Ordinal)||relative.IndexOf(':')>=0&&!IsDrive(relative))throw new InvalidOperationException("원격 이미지 주소는 읽지 않습니다.");
            string candidate=Path.GetFullPath(Path.IsPathRooted(relative)?relative:Path.Combine(root,relative));RequireDrive(candidate);
            string boundary=root.EndsWith(Path.DirectorySeparatorChar.ToString(),StringComparison.Ordinal)?root:root+Path.DirectorySeparatorChar;
            if(!candidate.StartsWith(boundary,StringComparison.OrdinalIgnoreCase)||candidate.IndexOf(':',2)>=0)throw new InvalidOperationException("이미지 폴더 범위를 벗어났습니다.");
            RejectReparse(candidate);
            using(var input=new FileStream(candidate,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var final=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(input.SafeFileHandle,final,(uint)final.Capacity,0);
                if(length==0||length>=final.Capacity)throw new IOException("이미지 실제 경로를 확인하지 못했습니다.");
                string actual=final.ToString();if(actual.StartsWith("\\\\?\\",StringComparison.Ordinal))actual=actual.Substring(4);
                if(!String.Equals(Path.GetFullPath(actual),candidate,StringComparison.OrdinalIgnoreCase))throw new IOException("이미지 경로가 변경되었습니다.");
                RejectReparse(candidate);if(!allowEmpty&&input.Length==0||input.Length>maximumBytes)throw new InvalidDataException("로컬 파일 크기 제한입니다.");
                using(var output=new MemoryStream()){var buffer=new byte[8192];int count;while((count=input.Read(buffer,0,Math.Min(buffer.Length,maximumBytes+1-(int)output.Length)))>0){output.Write(buffer,0,count);if(output.Length>maximumBytes)throw new InvalidDataException("로컬 파일 크기 제한입니다.");}return output.ToArray();}
            }
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IsDrive
        /// desc         : 드라이브 상대 경로를 제외한 로컬 drive 형식을 판별한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : URL scheme과 drive 문자를 혼동하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool IsDrive(string value){return value.Length>=3&&Char.IsLetter(value[0])&&value[1]==':'&&(value[2]=='\\'||value[2]=='/');}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequireDrive
        /// desc         : 로컬 절대 drive 경로만 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : UNC·장치 namespace 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RequireDrive(string value){if(!IsDrive(value))throw new InvalidOperationException("로컬 절대 이미지 폴더가 필요합니다.");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RejectReparse
        /// desc         : 존재하는 파일·상위 폴더의 junction·symbolic link를 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 선택한 안내 폴더 밖으로 우회하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void RejectReparse(string path){for(string current=path;!String.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("연결 이미지 경로는 읽지 않습니다.");}}
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownImageFrames
    /// desc         : WIC 호출 전 이미지 header·GIF 구조를 제한하고 disposal을 합성한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : canvas4MP·GIF60프레임/총4MP로 압축·프레임 폭탄 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    internal sealed class MarkdownImageFrames
    {
        internal readonly List<BitmapSource> Images=new List<BitmapSource>();
        internal readonly List<int> Delays=new List<int>();
        internal long Pixels;
        private const long PixelLimit=4*1024*1024;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Load
        /// desc         : PNG/JPEG/GIF의 사전 검증 뒤 stream 기반 WPF 비트맵만 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : UriSource·SVG·HTML·임의 codec 형식은 사용하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        internal static MarkdownImageFrames Load(byte[] bytes)
        {
            if(bytes.Length>=6&&Encoding.ASCII.GetString(bytes,0,6).StartsWith("GIF8",StringComparison.Ordinal))return LoadGif(bytes);
            int width=0,height=0;
            if(bytes.Length>=24&&bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})){width=Big32(bytes,16);height=Big32(bytes,20);}
            else if(bytes.Length>4&&bytes[0]==255&&bytes[1]==216){for(int at=2;at+8<bytes.Length;){if(bytes[at++]!=255)throw new InvalidDataException();while(at<bytes.Length&&bytes[at]==255)at++;if(at>=bytes.Length)break;int marker=bytes[at++];if(marker==217||marker==218)break;int size=Big16(bytes,at);if(size<2||at+size>bytes.Length)throw new InvalidDataException();if(marker>=192&&marker<=207&&marker!=196&&marker!=200&&marker!=204){height=Big16(bytes,at+3);width=Big16(bytes,at+5);break;}at+=size;}}
            CheckSize(width,height);
            var result=new MarkdownImageFrames();using(var stream=new MemoryStream(bytes,false)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=Math.Min(width,900);bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();result.Images.Add(bitmap);result.Delays.Add(100);result.Pixels=(long)bitmap.PixelWidth*bitmap.PixelHeight;}return result;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LoadGif
        /// desc         : GIF 블록을 먼저 순회한 후 투명 픽셀·background/previous disposal을 합성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : partial frame과 2·3 disposal이 다음 프레임을 오염시키지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static MarkdownImageFrames LoadGif(byte[] bytes)
        {
            if(bytes.Length<13)throw new InvalidDataException();int width=Little16(bytes,6),height=Little16(bytes,8);CheckSize(width,height);
            int at=13;uint background=0;int palette=0;if((bytes[10]&128)!=0){palette=3*(1<<((bytes[10]&7)+1));if(at+palette>bytes.Length)throw new InvalidDataException();int bg=bytes[11]*3;if(bg+2<palette)background=0xff000000u|((uint)bytes[at+bg]<<16)|((uint)bytes[at+bg+1]<<8)|bytes[at+bg+2];at+=palette;}
            var specs=new List<GifFrame>();int disposal=0,delay=100,transparent=-1;bool trailer=false;
            while(at<bytes.Length)
            {
                int marker=bytes[at++];if(marker==59){trailer=true;break;}
                if(marker==33){if(at>=bytes.Length)throw new InvalidDataException();int label=bytes[at++];if(label==249){if(at+6>bytes.Length||bytes[at]!=4||bytes[at+5]!=0)throw new InvalidDataException();disposal=(bytes[at+1]>>2)&7;delay=Math.Max(40,Math.Min(5000,Little16(bytes,at+2)*10));transparent=(bytes[at+1]&1)!=0?bytes[at+4]:-1;at+=6;}else SkipBlocks(bytes,ref at);continue;}
                if(marker!=44||at+9>bytes.Length)throw new InvalidDataException();
                var spec=new GifFrame{Left=Little16(bytes,at),Top=Little16(bytes,at+2),Width=Little16(bytes,at+4),Height=Little16(bytes,at+6),Disposal=disposal,Delay=delay,Background=transparent==bytes[11]?0:background};int flags=bytes[at+8];at+=9;
                if(spec.Width<=0||spec.Height<=0||spec.Left+spec.Width>width||spec.Top+spec.Height>height)throw new InvalidDataException();
                specs.Add(spec);if(specs.Count>60||(long)width*height*specs.Count>PixelLimit)throw new InvalidDataException("GIF 프레임 제한입니다.");
                if((flags&128)!=0)at+=3*(1<<((flags&7)+1));if(at>=bytes.Length||bytes[at]<2||bytes[at]>12)throw new InvalidDataException();at++;SkipBlocks(bytes,ref at);disposal=0;delay=100;transparent=-1;
            }
            if(!trailer||specs.Count==0)throw new InvalidDataException();
            var result=new MarkdownImageFrames();uint[] canvas=new uint[checked(width*height)];for(int i=0;i<canvas.Length;i++)canvas[i]=specs[0].Background;
            uint[] saved=null;GifFrame previous=null;
            using(var stream=new MemoryStream(bytes,false))
            {
                var decoder=new GifBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);if(decoder.Frames.Count!=specs.Count)throw new InvalidDataException();
                for(int index=0;index<specs.Count;index++)
                {
                    if(previous!=null&&previous.Disposal==2)for(int y=0;y<previous.Height;y++)for(int x=0;x<previous.Width;x++)canvas[(previous.Top+y)*width+previous.Left+x]=previous.Background;
                    else if(previous!=null&&previous.Disposal==3&&saved!=null)Array.Copy(saved,canvas,canvas.Length);
                    var spec=specs[index];saved=spec.Disposal==3?(uint[])canvas.Clone():null;var frame=decoder.Frames[index];if(frame.PixelWidth!=spec.Width||frame.PixelHeight!=spec.Height)throw new InvalidDataException();
                    var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);var pixels=new uint[checked(spec.Width*spec.Height)];converted.CopyPixels(pixels,spec.Width*4,0);
                    for(int y=0;y<spec.Height;y++)for(int x=0;x<spec.Width;x++){uint value=pixels[y*spec.Width+x];if((value>>24)!=0)canvas[(spec.Top+y)*width+spec.Left+x]=value;}
                    var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,canvas,width*4);image.Freeze();result.Images.Add(image);result.Delays.Add(spec.Delay);previous=spec;
                }
            }
            result.Pixels=(long)width*height*specs.Count;return result;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SkipBlocks
        /// desc         : 유한 파일 안의 GIF 길이 블록을 엄격히 건너뛴다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 잘린 데이터·무한 반복 없이 항상 offset 증가.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void SkipBlocks(byte[] bytes,ref int at){while(at<bytes.Length){int length=bytes[at++];if(length==0)return;if(at+length>bytes.Length)throw new InvalidDataException();at+=length;}throw new InvalidDataException();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CheckSize
        /// desc         : decoder를 호출하기 전에 논리 크기와 픽셀 수를 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 작은 압축 파일이 대형 비트맵을 할당하지 못하게 함.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void CheckSize(int width,int height){if(width<=0||height<=0||width>4096||height>4096||(long)width*height>PixelLimit)throw new InvalidDataException("이미지 픽셀 제한입니다.");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Big32
        /// desc         : PNG 크기를 overflow 없이 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 음수·상한 초과는 뒤 크기 검사로 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int Big32(byte[] bytes,int at){return unchecked((int)(((uint)bytes[at]<<24)|((uint)bytes[at+1]<<16)|((uint)bytes[at+2]<<8)|bytes[at+3]));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Big16
        /// desc         : JPEG 길이·크기를 범위 안에서 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 잘린 필드 거부.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int Big16(byte[] bytes,int at){if(at+1>=bytes.Length)throw new InvalidDataException();return bytes[at]*256+bytes[at+1];}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Little16
        /// desc         : GIF 구조의 유한 16비트 값을 읽는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : offset 범위를 먼저 확인.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static int Little16(byte[] bytes,int at){if(at+1>=bytes.Length)throw new InvalidDataException();return bytes[at]+bytes[at+1]*256;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : GifFrame
        /// desc         : 이미지 descriptor와 실제 지연·disposal만 저장한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : script·외부 링크·원문 메타데이터는 실행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class GifFrame { internal int Left,Top,Width,Height,Disposal,Delay;internal uint Background; }
    }
}
