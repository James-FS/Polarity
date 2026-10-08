#pragma once

#include "../include/NativeBrowser.h"
#include <condition_variable>
#include <mutex>
#include <utility>

// A clear must also wait for callbacks that copied a pointer before the clear.
class CallbackRegistry {
public:
    explicit CallbackRegistry(NB_Callbacks callbacks = {}) : m_callbacks(callbacks) {}

    void Set(const NB_Callbacks& callbacks)
    {
        const bool clearing = !callbacks.onNavigationStarting &&
                              !callbacks.onLoadCompleted &&
                              !callbacks.onTitleChanged &&
                              !callbacks.onWebMessageReceived &&
                              !callbacks.onParentDestroyed;
        std::unique_lock<std::mutex> lock(m_mutex);
        m_callbacks = callbacks;
        if (clearing)
            m_drained.wait(lock, [this] { return m_active == 0; });
    }

    template <typename Callback, typename Invoke>
    void Invoke(Callback NB_Callbacks::*member, Invoke&& invoke)
    {
        Callback callback;
        void* context;
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            callback = m_callbacks.*member;
            if (!callback) return;
            context = m_callbacks.ctx;
            ++m_active;
        }

        struct Completion {
            CallbackRegistry* owner;
            ~Completion() { owner->Finish(); }
        } completion{this};
        std::forward<Invoke>(invoke)(callback, context);
    }

private:
    void Finish()
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (--m_active == 0) m_drained.notify_all();
    }

    NB_Callbacks m_callbacks;
    std::mutex m_mutex;
    std::condition_variable m_drained;
    unsigned m_active = 0;
};
