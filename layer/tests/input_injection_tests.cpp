// Exercise action-space creation and location without a headset or XR runtime.
#include "../src/input_injection.cpp"

#include <cstdlib>
#include <cmath>
#include <fstream>
#include <iostream>

namespace {
std::optional<protocol::PoseInput> sample;
std::uintptr_t next_space = 100;
int failures = 0;

void expect(bool value, const char* message) {
  if (!value) { std::cerr << message << '\n'; ++failures; }
}

XRAPI_ATTR XrResult XRAPI_CALL create_space(XrSession, const XrActionSpaceCreateInfo*, XrSpace* space) {
  *space = reinterpret_cast<XrSpace>(next_space++);
  return XR_SUCCESS;
}

XRAPI_ATTR XrResult XRAPI_CALL locate_space(XrSpace, XrSpace, XrTime, XrSpaceLocation*) {
  return XR_SUCCESS;
}

XRAPI_ATTR XrResult XRAPI_CALL locate_views(XrSession, const XrViewLocateInfo*, XrViewState*,
    std::uint32_t, std::uint32_t* count, XrView*) {
  *count = 2;
  return XR_SUCCESS;
}

XRAPI_ATTR XrResult XRAPI_CALL get_proc(XrInstance, const char* name, PFN_xrVoidFunction* function) {
  if (std::strcmp(name, "xrCreateActionSpace") == 0)
    *function = reinterpret_cast<PFN_xrVoidFunction>(create_space);
  else if (std::strcmp(name, "xrLocateSpace") == 0)
    *function = reinterpret_cast<PFN_xrVoidFunction>(locate_space);
  else if (std::strcmp(name, "xrLocateViews") == 0)
    *function = reinterpret_cast<PFN_xrVoidFunction>(locate_views);
  else return XR_ERROR_FUNCTION_UNSUPPORTED;
  return XR_SUCCESS;
}
}  // namespace

void transport_send(const protocol::Message&) {}
std::optional<protocol::PoseInput> transport_latest_pose_input() { return sample; }
std::optional<protocol::HandTrackingInput> transport_latest_hand_tracking() { return {}; }

int main(int argc, char** argv) {
  if (argc == 3) {
    std::ifstream file(argv[1], std::ios::binary);
    const std::vector<char> chars((std::istreambuf_iterator<char>(file)), {});
    std::vector<std::byte> bytes;
    for (char value : chars) bytes.push_back(static_cast<std::byte>(value));
    const auto message = protocol::deserialize(bytes);
    const auto& input = std::get<protocol::PoseInput>(message.payload);
    expect(input.left.pose.orientation[0] == 0.1F, "C# left grip layout");
    expect(input.left.aim_pose.orientation[0] == 0.2F, "C# left aim layout");
    expect(input.right.pose.orientation[0] == 0.3F, "C# right grip layout");
    expect(input.right.aim_pose.orientation[0] == 0.4F, "C# right aim layout");
    expect(input.right.grip == 0.875F, "C# buttons/axes layout after added poses");
    expect(input.device_views[0].fov.angle_left == -0.91F &&
           input.device_views[1].fov.angle_right == 0.82F, "C# device eye FOV layout");
    const auto encoded = protocol::serialize(message);
    std::ofstream output(argv[2], std::ios::binary);
    output.write(reinterpret_cast<const char*>(encoded.data()), static_cast<std::streamsize>(encoded.size()));
    expect(output.good(), "write C++ interop fixture");
  }
  expect(component_from_binding_path("/user/hand/left/input/grip/pose") == Component::GripPose,
         "grip binding must stay distinct");
  expect(component_from_binding_path("/user/hand/right/input/aim/pose") == Component::AimPose,
         "aim binding must stay distinct");

  const auto instance = reinterpret_cast<XrInstance>(1);
  const auto session = reinterpret_cast<XrSession>(2);
  const auto action = reinterpret_cast<XrAction>(3);
  g_instances[instance] = InstanceData{get_proc, 10, 20};
  g_sessions[session] = instance;
  sample = protocol::PoseInput{};
  sample->left.pose.position = {1, 2, 3};
  sample->left.aim_pose.position = {4, 5, 6};
  sample->right.pose.position = {7, 8, 9};
  sample->right.aim_pose.position = {10, 11, 12};
  sample->left.aim_pose.orientation = {0.258819F, 0, 0, 0.965926F};
  sample->right.aim_pose.orientation = {0, -0.173648F, 0, 0.984808F};
  sample->left.pose.flags = protocol::OrientationValid;
  sample->left.aim_pose.flags = protocol::PositionValid;
  sample->right.pose.flags = protocol::OrientationTracked;
  sample->right.aim_pose.flags = 0; // Missing aim must not become a valid grip pose.

  for (const auto component : {Component::GripPose, Component::AimPose}) {
    ActionData data;
    data.instance = instance;
    data.bindings = {{Side::Left, component}, {Side::Right, component}};
    g_actions[action] = data;
    for (const auto side : {Side::Left, Side::Right}) {
      XrActionSpaceCreateInfo info{};
      info.type = XR_TYPE_ACTION_SPACE_CREATE_INFO;
      info.action = action;
      info.subactionPath = side == Side::Left ? 10 : 20;
      info.poseInActionSpace.orientation.w = 1;
      XrSpace space{};
      expect(hook_create_action_space(session, &info, &space) == XR_SUCCESS, "create space");
      XrSpaceLocation location{};
      location.type = XR_TYPE_SPACE_LOCATION;
      expect(hook_locate_space(space, XR_NULL_HANDLE, 0, &location) == XR_SUCCESS, "locate space");
      const auto& controller = side == Side::Left ? sample->left : sample->right;
      const auto& expected = component == Component::AimPose ? controller.aim_pose : controller.pose;
      expect(location.pose.position.x == expected.position[0], "position must match requested side and pose");
      expect(location.pose.orientation.x == expected.orientation[0] &&
             location.pose.orientation.y == expected.orientation[1] &&
             location.pose.orientation.w == expected.orientation[3], "rotation must match requested pose");
      expect(location.locationFlags == location_flags(expected), "preserve per-pose validity");
    }
  }
  // Unequal offsets and asymmetric FOVs must survive the simulator override.
  for (std::size_t eye = 0; eye < 2; ++eye) {
    auto& view = sample->device_views[eye];
    view.pose.position = {eye == 0 ? -0.030F : 0.035F, 1.2F, -0.4F};
    view.pose.flags = protocol::PositionValid | protocol::OrientationValid |
                      protocol::PositionTracked | protocol::OrientationTracked;
    view.fov = {.angle_left = -0.9F, .angle_right = 0.7F,
                .angle_up = 0.8F, .angle_down = -0.85F};
  }
  XrViewLocateInfo view_info{};
  view_info.type = XR_TYPE_VIEW_LOCATE_INFO;
  XrViewState view_state{};
  view_state.type = XR_TYPE_VIEW_STATE;
  std::array<XrView, 2> views{};
  std::uint32_t count{};
  expect(hook_locate_views(session, &view_info, &view_state, 2, &count, views.data()) == XR_SUCCESS,
         "locate device eye views");
  expect(views[0].pose.position.x == -0.030F && views[1].pose.position.x == 0.035F,
         "use actual device eye positions instead of fixed IPD");
  expect(views[0].fov.angleLeft == -0.9F && views[1].fov.angleRight == 0.7F,
         "use actual device FOV instead of simulator FOV");
  expect((view_state.viewStateFlags & XR_VIEW_STATE_POSITION_TRACKED_BIT) != 0,
         "device view tracking flags");
  // Render in a space rotated 90 degrees around Y and translated. Quest must
  // receive tracking-space poses, not these space-relative render poses.
  const auto render_space = reinterpret_cast<XrSpace>(900);
  constexpr float half_sqrt = 0.707106781F;
  g_spaces[render_space] = SpaceData{instance, session, SpaceSource::Local,
      XrPosef{.orientation = {0, half_sqrt, 0, half_sqrt}, .position = {2, 3, 4}}};
  std::array<XrCompositionLayerProjectionView, 2> submitted{};
  std::array<XrCompositionLayerProjectionView, 2> tracking{};
  submitted[0].pose = {.orientation = {0, 0, 0, 1}, .position = {1, 0, 0}};
  submitted[0].fov = {-0.9F, 0.7F, 0.8F, -0.85F};
  submitted[1] = submitted[0];
  expect(input_projection_to_tracking_space(render_space, submitted.data(), tracking.data(), 2),
         "convert projection reference space before streaming");
  expect(std::abs(tracking[0].pose.position.x - 2) < 0.00001F &&
         std::abs(tracking[0].pose.position.y - 3) < 0.00001F &&
         std::abs(tracking[0].pose.position.z - 3) < 0.00001F,
         "projection position includes reference-space rotation and translation");
  expect(std::abs(tracking[0].pose.orientation.y - half_sqrt) < 0.00001F &&
         std::abs(tracking[0].pose.orientation.w - half_sqrt) < 0.00001F,
         "projection orientation includes reference-space rotation");
  expect(tracking[0].fov.angleLeft == -0.9F, "projection conversion preserves FOV");
  expect(!input_projection_to_tracking_space(reinterpret_cast<XrSpace>(901), submitted.data(), tracking.data(), 2),
         "unknown projection space must not be silently treated as Quest tracking space");
  view_info.space = render_space;
  hook_locate_views(session, &view_info, &view_state, 2, &count, views.data());
  for (std::size_t eye = 0; eye < 2; ++eye) submitted[eye].pose = views[eye].pose;
  expect(input_projection_to_tracking_space(render_space, submitted.data(), tracking.data(), 2),
         "convert located eye views back to tracking space");
  expect(std::abs(tracking[0].pose.position.x + 0.030F) < 0.00001F &&
         std::abs(tracking[1].pose.position.x - 0.035F) < 0.00001F &&
         std::abs(tracking[0].pose.position.y - 1.2F) < 0.00001F &&
         std::abs(tracking[0].pose.position.z + 0.4F) < 0.00001F &&
         std::abs(tracking[0].pose.orientation.w - 1) < 0.00001F,
         "render-space conversion round trip preserves device eye poses");
  sample->device_views[1].pose.flags = 0;
  hook_locate_views(session, &view_info, &view_state, 2, &count, views.data());
  expect(view_state.viewStateFlags == 0, "invalid eye data must not become invented valid poses");
  return failures == 0 ? EXIT_SUCCESS : EXIT_FAILURE;
}
