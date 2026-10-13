using System;
using System.Runtime.InteropServices;

namespace NonVisualCalculus.Speech.Mac
{
    /// <summary>
    /// The Objective-C runtime calls the macOS speech code shares (<see cref="MacSpokenContent"/>,
    /// <see cref="MacSpeechStream"/>, <see cref="MacSystemVoice"/>): class and selector lookup,
    /// symbol lookup, the objc_msgSend shapes in use, and NSString conversion. Results are
    /// pointers, BOOLs, doubles or nothing, which plain objc_msgSend returns correctly on x86_64
    /// (the game runs under Rosetta) and on arm64 alike; a float or double argument rides a
    /// floating-point register, which objc_msgSend leaves alone.
    ///
    /// Strings cross as NUL-terminated UTF-8 byte arrays: the net35 profile has neither
    /// <c>UnmanagedType.LPUTF8Str</c> nor <c>Marshal.PtrToStringUTF8</c>, so
    /// <see cref="PrismNative.ToUtf8"/> and <see cref="PrismNative.FromUtf8"/> do the work. Only
    /// called on macOS.
    /// </summary>
    public static class ObjC
    {
        private const string Lib = "/usr/lib/libobjc.A.dylib";
        private const string System = "/usr/lib/libSystem.B.dylib";
        private const int RtldNow = 2;

        [DllImport(System)]
        private static extern IntPtr dlopen(string path, int mode);

        [DllImport(System)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private static readonly IntPtr RtldDefault = new IntPtr(-2);

        /// <summary>The address of a C symbol in any loaded image, such as a block class; Zero if none.</summary>
        public static IntPtr Symbol(string name)
        {
            return dlsym(RtldDefault, name);
        }

        [DllImport(Lib, EntryPoint = "objc_getClass")]
        public static extern IntPtr Class(string name);

        [DllImport(Lib, EntryPoint = "sel_registerName")]
        public static extern IntPtr Sel(string name);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, byte[] utf8);

        /// <summary>A message taking one float, such as the setter of a float property. Named apart
        /// from <see cref="Send(IntPtr, IntPtr, IntPtr)"/> because an int literal converts to both
        /// float and IntPtr.</summary>
        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendFloat(IntPtr receiver, IntPtr selector, float arg);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(
            IntPtr receiver,
            IntPtr selector,
            IntPtr arg1,
            IntPtr arg2,
            IntPtr arg3
        );

        /// <summary>A message taking a double then an unsigned int, such as
        /// initStandardFormatWithSampleRate:channels:. The double rides a floating-point register
        /// and the int an integer one, so their order in the selector does not affect placement.</summary>
        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern IntPtr SendDoubleUInt(
            IntPtr receiver,
            IntPtr selector,
            double arg1,
            uint arg2
        );

        /// <summary>A message returning a double, such as AVAudioFormat.sampleRate.</summary>
        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        public static extern double SendReturningDouble(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBool(IntPtr receiver, IntPtr selector);

        [DllImport(Lib, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool SendBool(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(Lib, EntryPoint = "objc_autoreleasePoolPush")]
        public static extern IntPtr AutoreleasePoolPush();

        [DllImport(Lib, EntryPoint = "objc_autoreleasePoolPop")]
        public static extern void AutoreleasePoolPop(IntPtr pool);

        private static readonly IntPtr SelRetain = Sel("retain");
        private static readonly IntPtr SelRelease = Sel("release");
        private static readonly IntPtr SelCount = Sel("count");
        private static readonly IntPtr SelIsKindOfClass = Sel("isKindOfClass:");
        private static readonly IntPtr SelDescription = Sel("description");
        private static readonly IntPtr SelUtf8String = Sel("UTF8String");
        private static readonly IntPtr SelStringWithUtf8 = Sel("stringWithUTF8String:");

        /// <summary>Take a +1 reference on <paramref name="obj"/> (nil is left alone) and hand it back.</summary>
        public static IntPtr Retain(IntPtr obj)
        {
            if (obj != IntPtr.Zero)
            {
                Send(obj, SelRetain);
            }

            return obj;
        }

        /// <summary>Give up a +1 reference; nil is left alone.</summary>
        public static void Release(IntPtr obj)
        {
            if (obj != IntPtr.Zero)
            {
                Send(obj, SelRelease);
            }
        }

        /// <summary>The count of an NSArray or other collection (nil counts as 0).</summary>
        public static long Count(IntPtr collection)
        {
            return Send(collection, SelCount).ToInt64();
        }

        /// <summary>True when <paramref name="obj"/> is an instance of the class named.</summary>
        public static bool IsKindOf(IntPtr obj, string className)
        {
            return obj != IntPtr.Zero && SendBool(obj, SelIsKindOfClass, Class(className));
        }

        /// <summary>An autoreleased NSString holding <paramref name="text"/>, or nil for null.</summary>
        public static IntPtr NSString(string text)
        {
            return text == null
                ? IntPtr.Zero
                : Send(Class("NSString"), SelStringWithUtf8, PrismNative.ToUtf8(text));
        }

        /// <summary>The UTF-8 contents of an NSString, or of any other object's description.</summary>
        public static string ToManagedString(IntPtr obj)
        {
            if (obj == IntPtr.Zero)
            {
                return null;
            }

            if (!IsKindOf(obj, "NSString"))
            {
                obj = Send(obj, SelDescription);
            }

            return PrismNative.FromUtf8(Send(obj, SelUtf8String));
        }

        /// <summary>Allocate and init an instance of <paramref name="className"/>, owned (+1).</summary>
        public static IntPtr Alloc(string className, string initSelector)
        {
            IntPtr obj = Send(Send(Class(className), Sel("alloc")), Sel(initSelector));
            if (obj == IntPtr.Zero)
            {
                throw new InvalidOperationException(className + " " + initSelector + " returned nil");
            }

            return obj;
        }

        private static bool _loaded;

        /// <summary>Make the speech classes resolvable: the game links AppKit already; AVFoundation
        /// may not be resident yet.</summary>
        public static void LoadSpeechFrameworks()
        {
            if (_loaded)
            {
                return;
            }

            dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", RtldNow);
            dlopen("/System/Library/Frameworks/AVFoundation.framework/AVFoundation", RtldNow);
            _loaded = true;
        }
    }
}
