// Emulator.hpp

#ifndef IPASIM_EMULATOR_HPP
#define IPASIM_EMULATOR_HPP

#include <unicorn/unicorn.h>
#include <utility>

namespace ipasim {

class DynamicLoader;

template <typename T, typename F> struct HookHelper {
  struct DataTy {
    T *Instance;
    F T::*Handler;
  };

  template <typename FTy> struct FunctionTraits;

  template <typename RetTy, typename... ArgTys>
  struct FunctionTraits<RetTy(ArgTys...)> {
    static RetTy hook(ArgTys &&... Args, void *Data) {
      auto *D = reinterpret_cast<DataTy *>(Data);
      return (D->Instance->*(D->Handler))(std::forward<ArgTys>(Args)...);
    }
  };
};

class Emulator {
public:
  Emulator(DynamicLoader &Dyld) : UC(initUC()), Dyld(Dyld) {}
  Emulator(const Emulator &) = delete;
  Emulator(Emulator &&E) : UC(nullptr), Dyld(E.Dyld) { std::swap(UC, E.UC); }
  ~Emulator();

  uint32_t readReg(uc_arm_reg RegId);
  void writeReg(uc_arm_reg RegId, uint32_t Value);
  void mapMemory(uint64_t Addr, uint64_t Size, uc_prot Perms);
  void start(uint64_t Addr);
  void stop();
  template <typename F>
  void hook(uc_hook_type Type, F *Handler, void *Instance) {
    hook(Type, reinterpret_cast<void *>(Handler), Instance);
  }
  void hook(uc_hook_type Type, void *Handler, void *Instance);
  template <typename T, typename F>
  void hook(uc_hook_type Type, F T::*Handler, T *Instance) {
    using HH = HookHelper<T, F>;
    hook(Type, HH::template FunctionTraits<F>::hook,
         new typename HH::DataTy{Instance, Handler});
  }

private:
  uc_engine *UC;
  DynamicLoader &Dyld;

  static uc_engine *initUC();
  static void callUCStatic(uc_err Err);
  void callUC(uc_err Err);
};

} // namespace ipasim

// !defined(IPASIM_EMULATOR_HPP)
#endif
