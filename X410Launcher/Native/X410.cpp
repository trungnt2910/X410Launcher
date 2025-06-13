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

#ifdef _M_X64
        machine = IMAGE_FILE_MACHINE_AMD64;
        frame.AddrPC.Offset    = context.Rip;
        frame.AddrPC.Mode      = AddrModeFlat;
        frame.AddrFrame.Offset = context.Rbp;
        frame.AddrFrame.Mode   = AddrModeFlat;
        frame.AddrStack.Offset = context.Rsp;
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

#ifdef _M_X64
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
                : "m" (context.Rbx),
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
