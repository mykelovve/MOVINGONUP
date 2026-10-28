// WWE2K26_MatchCardRelease: optional companion for the exact MovingRenders 4.21.
//
// The original BinkClose hook (RVA 0xa9c0) clears only the selection table at
// 0x13090; match-card contexts (0x137e0, or the capacity companion's pool) stay
// occupied after their movie closes. Once every entry is stale, each new
// match-card movie is registered over entry 0, so later cards lose their
// tracking, still-picture hiding and hidden-layout decode skip. This companion
// re-points the original's BinkClose detour to a wrapper that clears the closing
// handle's contexts under the original lock, then calls the original hook. It
// also logs context-table diagnostics. Nothing on disk is changed.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <tlhelp32.h>
#include <bcrypt.h>
#include <stdint.h>
#include <string.h>
#include <wchar.h>

namespace {
HMODULE self_module = nullptr;
wchar_t log_path[MAX_PATH]{};
constexpr const char* kOriginalSha = "b752da3c9a9dc48dc377972a7183db0194c2b3b551074f7d0f0c9766432bf75c";
constexpr uintptr_t kImageSize = 0xf4f000;
constexpr uintptr_t kLock = 0x13068, kBinkModule = 0x13058, kCloseReady = 0xf414a0, kCloseHook = 0xa9c0;
constexpr uintptr_t kBaseSite = 0x12eb, kCapacitySite = 0xb6a6, kStride = 0xc0;
constexpr uintptr_t kHiddenSkips = 0xf41490, kLiveBink = 0xf414a4, kPeakBink = 0xf414a8;
constexpr unsigned kMaxContexts = 120;
constexpr wchar_t kConfig[] = L"WWE2K26_MatchCardRelease.cfg";
constexpr wchar_t kLog[] = L"WWE2K26_MatchCardRelease.log";
struct Thread { HANDLE handle; bool suspended; };
Thread threads[1024]{};
size_t thread_count = 0;
volatile uintptr_t plugin_base = 0;
volatile LONG released = 0;
using CloseFn = void (*)(void*);

bool Read(uintptr_t address, void* value, size_t size) {
    SIZE_T got=0;
    return ReadProcessMemory(GetCurrentProcess(),reinterpret_cast<void*>(address),value,size,&got) && got==size;
}
template<class T> bool Value(uintptr_t address,T* value) { return Read(address,value,sizeof(T)); }
bool Join(wchar_t* output,const wchar_t* root,const wchar_t* name) {
    size_t a=wcslen(root),b=wcslen(name);
    if(a+b+2>MAX_PATH) return false;
    memcpy(output,root,a*sizeof(wchar_t)); output[a++]=L'\\';
    memcpy(output+a,name,(b+1)*sizeof(wchar_t)); return true;
}
void Log(const char* message) {
    if(!*log_path) return;
    HANDLE file=CreateFileW(log_path,FILE_APPEND_DATA,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
    if(file==INVALID_HANDLE_VALUE) return;
    LARGE_INTEGER length{};
    if(GetFileSizeEx(file,&length) && length.QuadPart<4*1024*1024) {
        char text[640]; SYSTEMTIME now{}; GetSystemTime(&now);
        int size=wsprintfA(text,"%04u-%02u-%02u %02u:%02u:%02u UTC %s\r\n",now.wYear,now.wMonth,now.wDay,now.wHour,now.wMinute,now.wSecond,message);
        DWORD written=0; if(size>0) WriteFile(file,text,static_cast<DWORD>(size),&written,nullptr);
    }
    CloseHandle(file);
}
bool Enabled(const wchar_t* root) {
    wchar_t path[MAX_PATH]{}; if(!Join(path,root,kConfig)) return false;
    HANDLE f=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,nullptr);
    if(f==INVALID_HANDLE_VALUE) return false;
    char text[64]{}; DWORD got=0; LARGE_INTEGER length{};
    bool ok=GetFileSizeEx(f,&length) && length.QuadPart>0 && length.QuadPart<64
        && ReadFile(f,text,static_cast<DWORD>(length.QuadPart),&got,nullptr) && got==length.QuadPart;
    CloseHandle(f); if(!ok) return false;
    while(got && (text[got-1]=='\r'||text[got-1]=='\n'||text[got-1]==' '||text[got-1]=='\t')) text[--got]=0;
    return !strcmp(text,"enabled 1");
}
bool OriginalFile(HMODULE module) {
    wchar_t path[MAX_PATH]{}; DWORD size=GetModuleFileNameW(module,path,MAX_PATH);
    if(!size||size>=MAX_PATH) return false;
    HANDLE file=CreateFileW(path,GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,nullptr);
    if(file==INVALID_HANDLE_VALUE) return false;
    static unsigned char bytes[69632]; unsigned char digest[32],object[1024]; DWORD got=0,object_size=0,returned=0;
    LARGE_INTEGER length{};
    bool ok=GetFileSizeEx(file,&length)&&length.QuadPart==sizeof(bytes)
        && ReadFile(file,bytes,sizeof(bytes),&got,nullptr)&&got==sizeof(bytes);
    CloseHandle(file);
    BCRYPT_ALG_HANDLE algorithm=nullptr; BCRYPT_HASH_HANDLE hash=nullptr;
    ok=ok && BCryptOpenAlgorithmProvider(&algorithm,BCRYPT_SHA256_ALGORITHM,nullptr,0)>=0
        && BCryptGetProperty(algorithm,BCRYPT_OBJECT_LENGTH,reinterpret_cast<PUCHAR>(&object_size),sizeof(object_size),&returned,0)>=0
        && object_size<=sizeof(object)
        && BCryptCreateHash(algorithm,&hash,object,object_size,nullptr,0,0)>=0
        && BCryptHashData(hash,bytes,sizeof(bytes),0)>=0
        && BCryptFinishHash(hash,digest,sizeof(digest),0)>=0;
    if(hash) BCryptDestroyHash(hash);
    if(algorithm) BCryptCloseAlgorithmProvider(algorithm,0);
    if(!ok) return false;
    const char hex[]="0123456789abcdef"; char actual[65]{};
    for(size_t i=0;i<32;++i) { actual[2*i]=hex[digest[i]>>4]; actual[2*i+1]=hex[digest[i]&15]; }
    return !strcmp(actual,kOriginalSha);
}
bool NativeImage(uintptr_t base) {
    IMAGE_DOS_HEADER dos{}; IMAGE_NT_HEADERS64 pe{}; char marker[19]{};
    return Value(base,&dos)&&dos.e_magic==IMAGE_DOS_SIGNATURE&&dos.e_lfanew>0&&dos.e_lfanew<0x1000
        &&Value(base+dos.e_lfanew,&pe)&&pe.Signature==IMAGE_NT_SIGNATURE
        &&pe.FileHeader.Machine==IMAGE_FILE_MACHINE_AMD64&&pe.OptionalHeader.Magic==IMAGE_NT_OPTIONAL_HDR64_MAGIC
        &&pe.OptionalHeader.SizeOfImage==kImageSize&&Read(base+0xe0e0,marker,sizeof(marker))
        &&!memcmp(marker,"MOVINGRENDERS-4.21",18);
}
// The live table base and size, read from the original's own registration code:
// either the original 16-entry table or the capacity companion's 120-entry pool.
bool ContextTable(uintptr_t base,uintptr_t* table,unsigned* capacity) {
    unsigned char lea[7]{},cmp[4]{};
    if(!Read(base+kBaseSite,lea,sizeof(lea))||lea[0]!=0x48||lea[1]!=0x8d||lea[2]!=0x0d) return false;
    if(!Read(base+kCapacitySite,cmp,sizeof(cmp))||cmp[0]!=0x48||cmp[1]!=0x83||cmp[2]!=0xf9) return false;
    if(cmp[3]!=0x10&&cmp[3]!=0x78) return false;
    int32_t displacement=0; memcpy(&displacement,lea+3,4);
    *table=base+kBaseSite+7+static_cast<intptr_t>(displacement);
    *capacity=cmp[3];
    return true;
}
// Installed as the target of the original's BinkClose detour.
extern "C" void ReleaseThenClose(void* handle) {
    uintptr_t base=plugin_base;
    if(handle&&base) {
        auto* lock=reinterpret_cast<CRITICAL_SECTION*>(base+kLock);
        EnterCriticalSection(lock);
        uintptr_t table=0; unsigned capacity=0;
        if(ContextTable(base,&table,&capacity))
            for(unsigned i=0;i<capacity;++i) {
                volatile uintptr_t* entry=reinterpret_cast<volatile uintptr_t*>(table+i*kStride);
                if(*entry!=reinterpret_cast<uintptr_t>(handle)) continue;
                // Registration zero-fills a reused entry the same way.
                for(unsigned word=0;word<kStride/sizeof(uintptr_t);++word) entry[word]=0;
                InterlockedIncrement(&released);
            }
        LeaveCriticalSection(lock);
    }
    if(base) reinterpret_cast<CloseFn>(base+kCloseHook)(handle);
}
void Resume() {
    for(size_t i=0;i<thread_count;++i) if(threads[i].suspended) { ResumeThread(threads[i].handle); threads[i].suspended=false; }
    for(size_t i=0;i<thread_count;++i) CloseHandle(threads[i].handle);
    thread_count=0;
}
// Stops every other thread; refuses while any is executing the detour bytes.
bool Freeze(uintptr_t detour) {
    HANDLE snapshot=CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD,0);
    if(snapshot==INVALID_HANDLE_VALUE) return false;
    THREADENTRY32 row{}; row.dwSize=sizeof(row); bool ok=Thread32First(snapshot,&row)!=FALSE;
    DWORD process=GetCurrentProcessId(),self=GetCurrentThreadId();
    if(ok) do {
        if(row.th32OwnerProcessID!=process||row.th32ThreadID==self) continue;
        if(thread_count==1024) { ok=false; break; }
        HANDLE thread=OpenThread(THREAD_SUSPEND_RESUME|THREAD_GET_CONTEXT|THREAD_QUERY_INFORMATION,FALSE,row.th32ThreadID);
        if(!thread) { if(GetLastError()==ERROR_INVALID_PARAMETER) continue; ok=false; break; }
        threads[thread_count++]={thread,false};
    } while(Thread32Next(snapshot,&row));
    CloseHandle(snapshot);
    if(!ok) { Resume(); return false; }
    for(size_t i=0;i<thread_count;++i) {
        if(SuspendThread(threads[i].handle)==static_cast<DWORD>(-1)) { Resume(); return false; }
        threads[i].suspended=true;
        CONTEXT context{}; context.ContextFlags=CONTEXT_CONTROL;
        if(!GetThreadContext(threads[i].handle,&context)||(context.Rip>=detour&&context.Rip<detour+14)) { Resume(); return false; }
    }
    return true;
}
bool Detour(uintptr_t close,uintptr_t* target) {
    unsigned char bytes[14]{};
    if(!Read(close,bytes,sizeof(bytes))||memcmp(bytes,"\xff\x25\0\0\0\0",6)) return false;
    memcpy(target,bytes+6,8); return true;
}
enum class Applied { Success, Busy, Mismatch, Protection };
Applied Commit(uintptr_t base,uintptr_t close) {
    if(!Freeze(close)) return Applied::Busy;
    uintptr_t target=0;
    if(!Detour(close,&target)||target!=base+kCloseHook) { Resume(); return Applied::Mismatch; }
    DWORD previous=0;
    if(!VirtualProtect(reinterpret_cast<void*>(close),14,PAGE_EXECUTE_READWRITE,&previous)) { Resume(); return Applied::Protection; }
    uintptr_t wrapper=reinterpret_cast<uintptr_t>(&ReleaseThenClose);
    memcpy(reinterpret_cast<void*>(close+6),&wrapper,8);
    FlushInstructionCache(GetCurrentProcess(),reinterpret_cast<void*>(close),14);
    DWORD ignored=0; VirtualProtect(reinterpret_cast<void*>(close),14,previous,&ignored);
    Resume(); return Applied::Success;
}
struct Snapshot { uintptr_t handle; int32_t player, layout; int32_t first_frame, marker; uintptr_t node; int32_t scale_offset; };
void Report(uintptr_t base) {
    static Snapshot last[kMaxContexts]{};
    static unsigned last_count=~0u, last_capacity=0; static LONG last_released=-1; static LONG last_skips=-1;
    Snapshot now[kMaxContexts]{}; unsigned count=0,capacity=0; uintptr_t table=0;
    auto* lock=reinterpret_cast<CRITICAL_SECTION*>(base+kLock);
    if(!TryEnterCriticalSection(lock)) return;
    if(ContextTable(base,&table,&capacity)&&capacity<=kMaxContexts)
        for(unsigned i=0;i<capacity;++i) {
            const unsigned char* entry=reinterpret_cast<const unsigned char*>(table+i*kStride);
            Snapshot& s=now[i];
            memcpy(&s.handle,entry,8); memcpy(&s.player,entry+8,4); memcpy(&s.layout,entry+0xc,4);
            memcpy(&s.first_frame,entry+0x10,4); memcpy(&s.marker,entry+0x14,4);
            memcpy(&s.node,entry+0x28,8); memcpy(&s.scale_offset,entry+0x30,4);
            if(s.handle) ++count;
        }
    LeaveCriticalSection(lock);
    LONG skips=0; Value(base+kHiddenSkips,&skips);
    LONG live=0,peak=0; Value(base+kLiveBink,&live); Value(base+kPeakBink,&peak);
    char text[640];
    if(count!=last_count||released!=last_released||skips!=last_skips||capacity!=last_capacity) {
        wsprintfA(text,"contexts in use %u/%u; released on close %ld; plugin hidden-layout decode skips %ld; live Bink handles %ld (peak %ld)%s",
            count,capacity,released,skips,live,peak,count&&count==capacity?"; TABLE FULL: the next new movie overwrites entry 0":"");
        Log(text);
        last_count=count; last_released=released; last_skips=skips; last_capacity=capacity;
    }
    for(unsigned i=0;i<capacity&&i<kMaxContexts;++i) {
        Snapshot& s=now[i];
        if(!memcmp(&s,&last[i],sizeof(s))) continue;
        if(s.handle) {
            const char* visibility="unknown";
            float pair[2]{};
            if(s.node&&s.scale_offset!=0x7fffffff&&Read(s.node+static_cast<intptr_t>(s.scale_offset)-0x54,pair,sizeof(pair)))
                visibility=(pair[0]==0.0f&&pair[1]==0.0f)?"hidden":"shown";
            wsprintfA(text,"  entry %u: layout L%02ld player %ldP handle %08lx%08lx node %s scale-field %s still-marker %s first-frame %ld layer %s",
                i,static_cast<long>(s.layout),static_cast<long>(s.player),static_cast<unsigned long>(s.handle>>32),static_cast<unsigned long>(s.handle&0xffffffffu),
                s.node?"bound":"NOT bound",s.scale_offset!=0x7fffffff&&s.node?"found":"not found",s.marker?"applied":"not applied",
                static_cast<long>(s.first_frame),visibility);
        } else wsprintfA(text,"  entry %u: free",i);
        Log(text);
        last[i]=s;
    }
}
DWORD WINAPI Worker(void*) {
    wchar_t directory[MAX_PATH]{}; DWORD length=GetModuleFileNameW(self_module,directory,MAX_PATH);
    if(!length||length>=MAX_PATH) return 0;
    wchar_t* last=nullptr; for(wchar_t* p=directory;*p;++p) if(*p==L'\\') last=p;
    if(!last) return 0; *last=0;
    if(!Join(log_path,directory,kLog)) return 0;
    if(!Enabled(directory)) { Log("Disabled: enabled 1 configuration absent"); return 0; }
    Log("Loaded: waiting for the original MovingRenders 4.21 BinkClose hook");
    HMODULE pinned=nullptr;
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(&Worker),&pinned)) return 0;
    HMODULE original=nullptr; uintptr_t close=0;
    for(unsigned attempt=0;;++attempt) {
        if(attempt==480) { Log("Disabled: original BinkClose hook not ready"); return 0; }
        HMODULE found=GetModuleHandleW(L"WWE2K26_MovingRenders.dlp");
        if(!found) found=GetModuleHandleW(L"WWE2K26_MovingRenders.dll");
        if(found&&!original) {
            if(!NativeImage(reinterpret_cast<uintptr_t>(found))||!OriginalFile(found)) { Log("Disabled: unsupported original file or image"); return 0; }
            original=found;
        }
        LONG ready=0; uintptr_t bink=0,target=0;
        if(original&&found==original&&Value(reinterpret_cast<uintptr_t>(original)+kCloseReady,&ready)&&ready==1
            &&Value(reinterpret_cast<uintptr_t>(original)+kBinkModule,&bink)&&bink) {
            // Resolve the export while other threads run; Commit never takes the loader lock.
            close=reinterpret_cast<uintptr_t>(GetProcAddress(reinterpret_cast<HMODULE>(bink),"BinkClose"));
            if(close&&Detour(close,&target)&&target==reinterpret_cast<uintptr_t>(original)+kCloseHook) break;
        }
        Sleep(250);
    }
    uintptr_t base=reinterpret_cast<uintptr_t>(original);
    HMODULE pinned_original=nullptr;
    if(!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_PIN,reinterpret_cast<LPCWSTR>(base),&pinned_original)
        ||pinned_original!=original) { Log("Disabled: could not retain original"); return 0; }
    uintptr_t table=0; unsigned capacity=0;
    if(!ContextTable(base,&table,&capacity)) { Log("Disabled: context-table instructions not recognised"); return 0; }
    plugin_base=base;
    Applied result=Applied::Busy;
    for(unsigned attempt=0;attempt<40&&result==Applied::Busy;++attempt) { result=Commit(base,close); if(result==Applied::Busy) Sleep(50); }
    if(result!=Applied::Success) { plugin_base=0; Log("Disabled: BinkClose detour could not be re-pointed; original behaviour retained"); return 0; }
    char text[160]; wsprintfA(text,"ACTIVE: closed match-card movies now release their contexts (table of %u)",capacity); Log(text);
    for(;;) { Report(base); Sleep(1000); }
}
}

BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,LPVOID) {
    if(reason==DLL_PROCESS_ATTACH) {
        self_module=module; DisableThreadLibraryCalls(module);
        HANDLE worker=CreateThread(nullptr,0,Worker,nullptr,0,nullptr);
        if(worker) CloseHandle(worker);
    }
    return TRUE;
}
