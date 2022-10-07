using System;
using System.Diagnostics;
using ArcFaceLibrary;

namespace app
{
  class Program
  {
    static ArcFace model = new ArcFace();
    static CancellationTokenSource cts = new CancellationTokenSource();
    static async Task Main(string[] args)
    {
        try {
            if (args.Length == 0) {
              await test1(false);
              // test2();
            }
            else {
              var len = args.Length;
              if (len % 2 == 1) {
                Console.WriteLine ("Number of arguments in command line must be divisible by 2");
                return;
              }
              var TArray = new Task[len/2];
              for (int i = 0; i < len-1; i+=2) {
                string st1 = "images/" + args[i];
                string st2 = "images/" + args[i+1];
                TArray[i/2] = RunTask(st1, st2, cts);
              }
              await Task.WhenAll(TArray);
            }
        }
        catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
    static async Task test1 (bool withCancel = false) {
      const int len = 8;
      const int repeat = 1;
      var input = new string[len] {"DiCaprio1.png", "DiCaprio2.png", "DuaLipa1.png", "DuaLipa2.png",
                                "Obama1.png", "Obama2.png", "face1.png", "face2.png"};
      for (int i = 0; i < len; i++) {
        input[i] = "images/" + input[i];
      }

      model = new ArcFace();
      var st = new Stopwatch();
      st.Start();
      var TArray = new Task[repeat * (len/2)];
      for (int j = 0; j < repeat; j++) {
        for (int i = 0; i < len - 1; i+=2) {

          TArray[j * (len/2) + i/2] = RunTask(input[i], input[i+1], cts);
        }
      }
      if (withCancel) {
        await Task.Delay(70);
        cts.Cancel();
      }
      await Task.WhenAll(TArray);

      st.Stop();
      Console.WriteLine($"Elapsed Ms async: {st.ElapsedMilliseconds}");
    }
    static void test2 () {
      const int len = 8;
      const int repeat = 1;
      var input = new string[len] {"DiCaprio1.png", "DiCaprio2.png", "DuaLipa1.png", "DuaLipa2.png",
                                "Obama1.png", "Obama2.png", "face1.png", "face2.png"};
      for (int i = 0; i < len; i++) {
        input[i] = "images/" + input[i];
      }
      model = new ArcFace();
      var st = new Stopwatch();
      st.Start();
      for (int j = 0; j < repeat; j++) {
        for (int i = 0; i < len - 1; i+=2) {
          var img1 = File.ReadAllBytes(input[i]);
          var img2 = File.ReadAllBytes(input[i+1]);
          var List = model.Process(img1, img2);
          lock(model) {
            foreach (var elem in List)
                Console.WriteLine($"\t{elem.Item1}: {elem.Item2}");
          }
        }
      }
      st.Stop();
      Console.WriteLine($"Elapsed Ms sync: {st.ElapsedMilliseconds}");
    }
    static async Task RunTask (string img1, string img2, CancellationTokenSource cts)  {
      var b1 = await File.ReadAllBytesAsync(img1);
      var b2 = await File.ReadAllBytesAsync(img2);

      var List = await model.ProcessAsync(b1, b2, cts);
      lock(model) {
        foreach (var elem in List)
            Console.WriteLine($"\t{elem.Item1}: {elem.Item2}");
      }
    }
  }
}
