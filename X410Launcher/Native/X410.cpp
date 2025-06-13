#include <limits.h>
#include <wchar.h>
#include <windows.h>
#include <winnt.h>

extern "C"
{

#include <dbghelp.h>

}

#include <detours.h>

static decltype(&MessageBoxW) RealMessageBoxW = MessageBoxW;
static decltype(&RegisterClassExW) RealRegisterClassExW = RegisterClassExW;

int WINAPI HookMessageBoxW(HWND hWnd, LPCWSTR lpText, LPCWSTR lpCaption, UINT uType)
{
    static BOOL IsFirstMessage = TRUE;

    if (IsFirstMessage)
    {
        CONTEXT context = {};
        context.ContextFlags = CONTEXT_FULL;
        RtlCaptureContext(&context);

        DWORD machine;
        STACKFRAME64 frame = {};

        SymInitializeW(GetCurrentProcess(), NULL, TRUE);

#if defined(_M_X64)
        machine = IMAGE_FILE_MACHINE_AMD64;
        frame.AddrPC.Offset    = context.Rip;
        frame.AddrPC.Mode      = AddrModeFlat;
        frame.AddrFrame.Offset = context.Rbp;
        frame.AddrFrame.Mode   = AddrModeFlat;
        frame.AddrStack.Offset = context.Rsp;
        frame.AddrStack.Mode   = AddrModeFlat;
#elif defined(_M_ARM64)
        machine = IMAGE_FILE_MACHINE_ARM64;
        frame.AddrPC.Offset    = context.Pc;
        frame.AddrPC.Mode      = AddrModeFlat;
        frame.AddrFrame.Offset = context.Fp;
        frame.AddrFrame.Mode   = AddrModeFlat;
        frame.AddrStack.Offset = context.Sp;
        frame.AddrStack.Mode   = AddrModeFlat;
#else
#error Fill in frame info for this architecture!
#endif

        BOOL success = TRUE;

        SIZE_T szFrames = 3;
        for (SIZE_T i = 0; i < szFrames; ++i)
        {
            if (!StackWalk64(
                machine,
                GetCurrentProcess(), GetCurrentThread(),
                &frame,
                &context,
                NULL, NULL, NULL, NULL
            ))
            {
                success = FALSE;
                break;
            }
        }

        SymCleanup(GetCurrentProcess());

        if (success)
        {
            IsFirstMessage = FALSE;

#if defined(_M_X64)
            __asm__ volatile(
                "mov %0, %%rbx;"
                // "mov %1, %%rbp;"
                "mov %2, %%rsi;"
                "mov %3, %%rdi;"
                "mov %4, %%r12;"
                "mov %5, %%r13;"
                "mov %6, %%r14;"
                "mov %7, %%r15;"

                "mov %9, %%rcx;"
                "mov %1, %%rdx;"

                "mov %8, %%rsp;"

                // Only mess with rbp after everything else's done.
                "mov %%rdx, %%rbp;"

                "mov $1, %%rax;"

                "jmp *%%rcx;"
                :
                :   "m" (context.Rbx),
                    "m" (context.Rbp),
                    "m" (context.Rsi),
                    "m" (context.Rdi),
                    "m" (context.R12),
                    "m" (context.R13),
                    "m" (context.R14),
                    "m" (context.R15),

                    "m" (context.Rsp),
                    "m" (context.Rip)
                :
            );
#elif defined(_M_ARM64)
            __asm__ volatile(
                "ldr     x19, [%0, #0xa0]\n\t"
                "ldr     x20, [%0, #0xa8]\n\t"
                "ldr     x21, [%0, #0xb0]\n\t"
                "ldr     x22, [%0, #0xb8]\n\t"
                "ldr     x23, [%0, #0xc0]\n\t"
                "ldr     x24, [%0, #0xc8]\n\t"
                "ldr     x25, [%0, #0xd0]\n\t"
                "ldr     x26, [%0, #0xd8]\n\t"
                "ldr     x27, [%0, #0xe0]\n\t"
                "ldr     x28, [%0, #0xe8]\n\t"

                "ldr     x29, [%0, #0xf0]\n\t"
                "ldr     x30, [%0, #0xf8]\n\t"

                "mov     x0, #0x1        \n\t"
                "mov     sp, %2          \n\t"
                "br      %1              \n\t"
                :
                : "r"(&context), "r"(context.Pc), "r"(context.Sp)
                : "x19", "x20", "x21", "x22", "x23", "x24",
                  "x25", "x26", "x27", "x28", "x29", "x30"
            );
#else
#error Restore context for this architecture!
#endif

            __builtin_unreachable();
        }
    }

    return RealMessageBoxW(hWnd, lpText, lpCaption, uType);
}

ATOM HookRegisterClassExW(const PWNDCLASSEXW pWndClassEx)
{
    if (wcscmp(pWndClassEx->lpszClassName, L"X410_RootWin") == 0)
    {
        static WNDPROC pfnOriginalWndProc = pWndClassEx->lpfnWndProc;

        constexpr auto fnNewWndProc =
            [](HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam) -> LRESULT
        {
            if (uMsg == 33178 && wParam == 50 && lParam == 0)
            {
                return (1u << 16) | USHRT_MAX;
            }

            return pfnOriginalWndProc(hWnd, uMsg, wParam, lParam);
        };

        pWndClassEx->lpfnWndProc = fnNewWndProc;
    }

    return RealRegisterClassExW(pWndClassEx);
}

extern "C"
BOOL WINAPI
DllMain(HINSTANCE hinst, DWORD dwReason, LPVOID reserved)
{
    if (DetourIsHelperProcess())
    {
        return TRUE;
    }

    switch (dwReason)
    {
        case DLL_PROCESS_ATTACH:
            DetourRestoreAfterWith();

            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());
            DetourAttach(&(PVOID &)RealMessageBoxW, (PVOID)HookMessageBoxW);
            DetourAttach(&(PVOID &)RealRegisterClassExW, (PVOID)HookRegisterClassExW);
            DetourTransactionCommit();
        break;
        case DLL_PROCESS_DETACH:
            DetourTransactionBegin();
            DetourUpdateThread(GetCurrentThread());
            DetourDetach(&(PVOID &)RealRegisterClassExW, (PVOID)HookRegisterClassExW);
            DetourDetach(&(PVOID &)RealMessageBoxW, (PVOID)HookMessageBoxW);
            DetourTransactionCommit();
        break;
    }

    return TRUE;
}
