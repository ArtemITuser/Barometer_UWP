using System;
namespace Windows.Graphics.Imaging { public enum BitmapAlphaMode{Ignore,Premultiply,Straight} }
namespace Windows.Storage.Streams { public partial interface IBuffer{} }
namespace Windows.Web.Http {
  public class ByteArrayContent2 {}
}

namespace Windows.Storage.Streams {
  public partial interface IBuffer { uint Length{get;} }
  public sealed class DataStoreBuffer : IBuffer { byte[] _d=new byte[0]; public uint Length=>(uint)_d.Length; public DataStoreBuffer(uint size){} public void CopyTo(byte[] b,uint c)=>Array.Copy(_d,b,Math.Min(c,(uint)_d.Length)); }
  public static class BufferOps { public static global::System.Threading.Tasks.Task<IBuffer> ToTask(this byte[] b)=>global::System.Threading.Tasks.Task.FromResult<IBuffer>(new DataStoreBuffer((uint)b.Length)); }
}

namespace Windows.Foundation {
  public interface IAsyncOperation2<T> { T GetResults(); event global::System.Action<object> Completed; global::System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter(); }
  public class AsyncOp2<T> : IAsyncOperation2<T> { public T GetResults()=>default(T); public event global::System.Action<object> Completed; public global::System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter()=>global::System.Threading.Tasks.Task.Run(()=>GetResults()).GetAwaiter(); }
}

namespace Windows.Web.Http {
  public class ByteArrayContent : IHttpContent { byte[] _b; public ByteArrayContent(byte[] b){_b=b??new byte[0];} public Windows.Web.Http.Headers.HttpContentHeaderCollection Headers{get;set;}=new Windows.Web.Http.Headers.HttpContentHeaderCollection(); public global::System.Threading.Tasks.Task<string> ReadAsStringAsync()=>global::System.Threading.Tasks.Task.FromResult(""); public global::System.Threading.Tasks.Task<Windows.Storage.Streams.IBuffer> ReadAsBufferAsync()=>global::System.Threading.Tasks.Task.FromResult<Windows.Storage.Streams.IBuffer>(new Windows.Storage.Streams.DataStoreBuffer((uint)_b.Length)); public global::System.Threading.Tasks.Task<global::System.IO.Stream> ReadAsStreamAsync()=>global::System.Threading.Tasks.Task.FromResult<global::System.IO.Stream>(new global::System.IO.MemoryStream(_b)); }
}
namespace Windows.Web.Http.Headers { public class HttpContentHeaderCollection { public object ContentType{get;set;} } }

namespace Windows.Storage.Streams {
  public static class StreamShimExt {
    public static global::System.IO.Stream AsStream(this IRandomAccessStream s){ var ms=new global::System.IO.MemoryStream(); var buf=new DataStoreBuffer(0); return ms; }
    public static global::System.Threading.Tasks.Task<byte[]> ReadAsByteArrayAsync(this Windows.Web.Http.IHttpContent c){ var s=c.ReadAsStreamAsync().Result; var ms=new global::System.IO.MemoryStream(); s.CopyTo(ms); return global::System.Threading.Tasks.Task.FromResult(ms.ToArray()); }
  }
}
namespace Windows.Web.Http.Headers {
  public static class HeaderShimExt { public static bool TryParseAdd(this Windows.Web.Http.Headers.HttpRequestHeaderValueCollection h,string v){ try{h.Add(v);}catch{} return true; } }
}


namespace Windows.System { namespace RuntimeNS { public class ProfileSettings { public static global::System.Threading.Tasks.Task<bool> IsRoamingEnabledAsync()=>global::System.Threading.Tasks.Task.FromResult(true); } } }
namespace Barometer_UWP {
  public class App {
    public static App Current { get; } = new App();
    public ServiceContainer Services { get; } = new ServiceContainer();
    
  }
}
namespace Windows.Graphics.Imaging {
  public partial class BitmapDecoder {
    public static global::System.Threading.Tasks.Task<BitmapDecoder> CreateAsync(object stream)=>global::System.Threading.Tasks.Task.FromResult(new BitmapDecoder());
    public uint PixelWidth{get;set;} public uint PixelHeight{get;set;}
    public global::System.Threading.Tasks.Task<BitmapTransform> GetTransformedAsync() => global::System.Threading.Tasks.Task.FromResult(new BitmapTransform());
  }
  public class BitmapTransform { public ExponentialDegreeTransform ExponentialDegree{get;set;} public ExponentialOrderByComponent ExponentialOrderBy{get;set;} }
  public class ExponentialDegreeTransform { public double Degree{get;set;} }
  public class ExponentialOrderByComponent { public static ExponentialOrderByComponent Default = new ExponentialOrderByComponent(); }
  public class PixelDataProvider { public byte[] DetachPixelData()=>new byte[0]; public global::System.Threading.Tasks.Task<PixelDataProvider> GetPixelDataAsync(uint x,uint y,uint w,uint h,BitmapTransform t,BitmapAlphaMode a,BmpPropertySet p)=>global::System.Threading.Tasks.Task.FromResult(new PixelDataProvider()); }
  public enum BmpPropertySet { Nearest }
}
namespace Windows.Storage.Streams {
  public partial class DataWriter { }
}
namespace Windows.Web.Http.Headers {
  public partial class HttpRequestHeaderValueCollection { public HttpRequestHeaderValueCollection Accept{get;} }
}
namespace Windows.UI.Xaml.Controls {
  public partial class Canvas { public static void SetLeft(UIElement e,double v){} public static void SetTop(UIElement e,double v){} }
}
namespace Windows.UI.Xaml {
  public enum DispatcherPriority { Low, Normal, High }
  public class DispatcherScheduler {}
}

namespace System.IO.Packaging {
  public class Package : IDisposable { public static Package Open(object s, object m)=>new Package(); public PackagePart CreatePart(Uri r,string ct,object comp)=>new PackagePart(); public PackagePart GetPart(Uri r)=>new PackagePart(); public global::System.Collections.Generic.IEnumerable<PackagePart> GetParts()=>new global::System.Collections.Generic.List<PackagePart>(); public void Flush(){} public void Dispose(){} }
  public class PackagePart { public Uri Uri{get;set;} public string ContentType{get;set;} public global::System.IO.Stream GetStream(object m,object a){return new global::System.IO.MemoryStream();} public global::System.Collections.Generic.IEnumerable<PackageRelationship> GetRelationshipsByType(string t)=>new global::System.Collections.Generic.List<PackageRelationship>(); public void Delete(){} }
  public class PackageRelationship { public Uri TargetUri{get;set;} }
  public enum CompressionOption { Normal, Fastest, Maximum, NotEnabled }
  public enum FileMode2 {}
}
namespace Windows.Graphics.Imaging {
  public partial class BitmapDecoder {
    public global::System.Threading.Tasks.Task<PixelDataProvider> GetPixelDataAsync(BitmapPixelFormat pf, BitmapAlphaMode am, BitmapTransform t, ExponentialOrderByComponent o, BmpPropertySet p)=>global::System.Threading.Tasks.Task.FromResult(new PixelDataProvider());
    public global::System.Threading.Tasks.Task<PixelDataProvider> GetPixelDataAsync(BitmapPixelFormat pf, BitmapAlphaMode am, BitmapTransform t, ExponentialOrderByComponent o, ExponentialDegreeTransform d)=>global::System.Threading.Tasks.Task.FromResult(new PixelDataProvider());
  }
}

namespace Windows.Storage {
  public static class FileIOExt { public static async global::System.Threading.Tasks.Task<byte[]> ReadBytesAsync(this StorageFile f){ var b=await f.ReadBufferAsync(); var d=new byte[b.Length]; using(var r=new Windows.Storage.Streams.DataReader(b)) r.ReadBytes(d); return d; } }
}
