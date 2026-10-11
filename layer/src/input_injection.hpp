#pragma once

#include <openxr/openxr.h>
#include <cstdint>

void input_register_instance(XrInstance instance, PFN_xrGetInstanceProcAddr gipa);
void input_unregister_instance(XrInstance instance);
void input_register_session(XrSession session, XrInstance instance);
void input_unregister_session(XrSession session);
[[nodiscard]] bool input_get_proc_addr(const char* name, PFN_xrVoidFunction* function);
// Projection poses are relative to their submitted space. The Quest receiver
// needs poses in the same tracking space as its forwarded device input.
[[nodiscard]] bool input_projection_to_tracking_space(
    XrSpace space, const XrCompositionLayerProjectionView* source,
    XrCompositionLayerProjectionView* destination, std::uint32_t count);
