using System;
using System.Linq;
using System.Collections.Generic;

using Foundation;
using UIKit;
using MonoTouch.NUnit.UI;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LockTest
{
	// My favorite links about async/await:
	// - Async and Await http://blog.stephencleary.com/2012/02/async-and-await.html
	// - StartNew is Dangerous http://blog.stephencleary.com/2013/08/startnew-is-dangerous.html
	// - Don't Block on Async Code http://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
	// - What is the SynchronizationContext? http://www.codeproject.com/Articles/31971/Understanding-SynchronizationContext-Part-I
	// - No SynchronizationContext in Console Apps - http://blogs.msdn.com/b/pfxteam/archive/2012/01/20/10259049.aspx

	[TestFixture]
	public class MyTests
	{
		[Test]
		public void Test_01_ConfigureAwait_FALSE_without_UI_access_prevents_deadlock()
		{
			Console.WriteLine("Execute async code, wait synchronously, do not capture the context and don't access the UI: this won't deadlock!");

			// Try to sleep a couple of seconds and tell the method to use ConfigureAwait(false) and do not access the UI...
			var testTask = ProcessTaskAsync(delaySeconds: 3, configureAwait: false, accessUi: false);
			// ...but block (this will block the UI thread, which is our synchronization context).
			testTask.Wait();
			// We will get here after a couple of seconds. Test succeeded.

			Console.WriteLine("Done.");
		}

		[Test]
		public void Test_02_ConfigureAwait_FALSE_with_UI_access_causes_exception()
		{
			Console.WriteLine("Execute async code, wait synchronously, do not capture the context and access the UI: this wouldn't deadlock, but it throws an exception!");

			// Try to sleep a couple of seconds and tell the method to use ConfigureAwait(false) (which will prevent the deadlock, see Test_01), but try to access the UI...
			var testTask = ProcessTaskAsync(delaySeconds: 3, configureAwait: false, accessUi: true);
			// ...but block (this will block the UI thread, which is our synchronization context).
			testTask.Wait();

			// We don't get here - the test will fail because accessing the UI from a non-UI thread causes an exception.

			Console.WriteLine("Done.");
		}

		[Test]
		public void Test_03_ConfigureAwait_TRUE_causes_deadlock()
		{
			Console.WriteLine("Execute async code, wait synchronously, capture the context: this will deadlock, that's why it is the last test ;-)");

			// Try to sleep a couple of seconds and tell the method to use ConfigureAwait(true) - it does not matter if we access the UI or not, we'll never get to that code.
			var testTask = ProcessTaskAsync(delaySeconds: 3, configureAwait: true, accessUi: false);
			// ...but block (this will block the UI thread, which is our synchronization context).
			testTask.Wait();
			// We will get here after a couple of seconds. Test succeeded.

			Console.WriteLine("Done.");
		}

		/// <summary>
		/// Helper to simulate an async operation. After the operation, the UI will not be accessed.
		/// </summary>
		static async Task ProcessTaskAsync(int delaySeconds, bool configureAwait, bool accessUi)
		{
			// We enter the method synchronously and then "await" is encountered, that's when things start to become async.
			// The current SynchronizationContext is implicitly captured (in our case, the UI thread).
			await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(configureAwait);

			// After the await we will be...
			//     - Back on the previous context (UI thread) if configureAwait == true.
			//        All code following from here on will be executed within the captured context (calling the Post() method on the SynchronizationContext), but the problem is:
			//        the UI thread is blocked! It is using Task.Wait() to wait for this async operation to finish, so it cannot execute our code => deadlock.

			//     - Running on the thread pool if configureAwait == false.
			//        We essentially tell the compiler: "we don't need the context to be captured! There is no need to execute whatever comes here on the UI thread!"
			//        That's fine as ong we don't perform UI related work and for example just use Console.WriteLine().
			Console.WriteLine("Task awaited");

			if(accessUi)
			{
				// Results in a UIKitThreadAccessException if configureAwait == false, because UIKit does not allow manipulation of the UI from any other threads!
				UIApplication.SharedApplication.Windows[0].RootViewController.PresentViewController(new UIViewController(), true, null);
			}
		}
	}

	[Register ("UnitTestAppDelegate")]
	public partial class UnitTestAppDelegate : UIApplicationDelegate
	{
		UIWindow window;
		TouchRunner runner;

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			window = new UIWindow (UIScreen.MainScreen.Bounds);
			runner = new TouchRunner (window);
			runner.Add (System.Reflection.Assembly.GetExecutingAssembly ());
			window.RootViewController = new UINavigationController (runner.GetViewController ());
			window.MakeKeyAndVisible ();
			return true;
		}
	}
}

