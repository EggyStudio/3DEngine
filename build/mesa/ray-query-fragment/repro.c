// Draws frag.spv, a fragment shader that makes one ray query against a scene of one triangle,
// offscreen into a 64 by 64 image, whose middle pixel is red where the ray met the triangle and
// blue where it did not, to report a crash of lavapipe's to Mesa. The scene is bound at the set and
// the binding the program's two arguments give, beside a uniform buffer of lights at binding 22 and
// a storage buffer at 29 in the same set, as 3DEngine's model pass reads them, and every set before
// it is empty.
//
//   cc repro.c -o repro -lvulkan && ./repro <set> <binding>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <vulkan/vulkan.h>

#define CHECK(call) do { VkResult result_ = (call); if (result_ != VK_SUCCESS) { \
    fprintf(stderr, "%s failed with %d at line %d\n", #call, result_, __LINE__); exit(1); } } while (0)

enum { Size = 64 };

static VkPhysicalDevice physical;
static VkDevice device;
static PFN_vkCreateAccelerationStructureKHR createStructure;
static PFN_vkGetAccelerationStructureBuildSizesKHR buildSizes;
static PFN_vkCmdBuildAccelerationStructuresKHR cmdBuild;
static PFN_vkGetAccelerationStructureDeviceAddressKHR structureAddress;

typedef struct { VkBuffer buffer; VkDeviceMemory memory; VkDeviceAddress address; void *mapped; } Buffer;

static uint32_t memoryType(uint32_t bits, VkMemoryPropertyFlags wanted)
{
    VkPhysicalDeviceMemoryProperties properties;
    vkGetPhysicalDeviceMemoryProperties(physical, &properties);
    for (uint32_t i = 0; i < properties.memoryTypeCount; i++)
        if ((bits & (1u << i)) && (properties.memoryTypes[i].propertyFlags & wanted) == wanted) return i;
    fprintf(stderr, "no memory type fits\n");
    exit(1);
}

// Every buffer is mapped and has an address, which lavapipe's memory always allows.
static Buffer makeBuffer(VkDeviceSize size, VkBufferUsageFlags usage)
{
    Buffer made = {0};
    VkBufferCreateInfo info = { .sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO, .size = size,
        .usage = usage | VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT };
    CHECK(vkCreateBuffer(device, &info, NULL, &made.buffer));
    VkMemoryRequirements needs;
    vkGetBufferMemoryRequirements(device, made.buffer, &needs);
    VkMemoryAllocateFlagsInfo flags = { .sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO,
        .flags = VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT };
    VkMemoryAllocateInfo allocate = { .sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO, .pNext = &flags,
        .allocationSize = needs.size,
        .memoryTypeIndex = memoryType(needs.memoryTypeBits, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) };
    CHECK(vkAllocateMemory(device, &allocate, NULL, &made.memory));
    CHECK(vkBindBufferMemory(device, made.buffer, made.memory, 0));
    CHECK(vkMapMemory(device, made.memory, 0, VK_WHOLE_SIZE, 0, &made.mapped));
    VkBufferDeviceAddressInfo address = { .sType = VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO, .buffer = made.buffer };
    made.address = vkGetBufferDeviceAddress(device, &address);
    return made;
}

static VkAccelerationStructureKHR build(VkCommandBuffer cmd, VkAccelerationStructureTypeKHR type,
    const VkAccelerationStructureGeometryKHR *geometry, uint32_t count)
{
    VkAccelerationStructureBuildGeometryInfoKHR info = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR,
        .type = type, .flags = VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR,
        .mode = VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR, .geometryCount = 1, .pGeometries = geometry };
    VkAccelerationStructureBuildSizesInfoKHR sizes = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR };
    buildSizes(device, VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR, &info, &count, &sizes);
    Buffer storage = makeBuffer(sizes.accelerationStructureSize, VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR);
    Buffer scratch = makeBuffer(sizes.buildScratchSize + 256, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    VkAccelerationStructureCreateInfoKHR create = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR,
        .buffer = storage.buffer, .size = sizes.accelerationStructureSize, .type = type };
    VkAccelerationStructureKHR structure;
    CHECK(createStructure(device, &create, NULL, &structure));
    info.dstAccelerationStructure = structure;
    info.scratchData.deviceAddress = (scratch.address + 255) & ~(VkDeviceAddress)255;
    VkAccelerationStructureBuildRangeInfoKHR range = { .primitiveCount = count };
    const VkAccelerationStructureBuildRangeInfoKHR *ranges = &range;
    cmdBuild(cmd, 1, &info, &ranges);
    return structure;
}

static void structureBarrier(VkCommandBuffer cmd, VkPipelineStageFlags to)
{
    VkMemoryBarrier barrier = { .sType = VK_STRUCTURE_TYPE_MEMORY_BARRIER,
        .srcAccessMask = VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR, .dstAccessMask = VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR };
    vkCmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, to, 0, 1, &barrier, 0, NULL, 0, NULL);
}

static VkShaderModule loadShader(const char *path)
{
    FILE *file = fopen(path, "rb");
    if (!file) { fprintf(stderr, "cannot read %s\n", path); exit(1); }
    fseek(file, 0, SEEK_END);
    long size = ftell(file);
    fseek(file, 0, SEEK_SET);
    uint32_t *code = malloc(size);
    if (fread(code, 1, size, file) != (size_t)size) { fprintf(stderr, "short read of %s\n", path); exit(1); }
    fclose(file);
    VkShaderModuleCreateInfo info = { .sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO, .codeSize = size, .pCode = code };
    VkShaderModule module;
    CHECK(vkCreateShaderModule(device, &info, NULL, &module));
    free(code);
    return module;
}

static void imageBarrier(VkCommandBuffer cmd, VkImage image, VkImageLayout from, VkImageLayout to,
    VkAccessFlags before, VkAccessFlags after, VkPipelineStageFlags fromStage, VkPipelineStageFlags toStage)
{
    VkImageMemoryBarrier barrier = { .sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER, .srcAccessMask = before, .dstAccessMask = after,
        .oldLayout = from, .newLayout = to, .srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED, .dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
        .image = image, .subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } };
    vkCmdPipelineBarrier(cmd, fromStage, toStage, 0, 0, NULL, 0, NULL, 1, &barrier);
}

int main(int argc, char **argv)
{
    uint32_t set = argc > 1 ? (uint32_t)atoi(argv[1]) : 0, binding = argc > 2 ? (uint32_t)atoi(argv[2]) : 0;

    VkApplicationInfo application = { .sType = VK_STRUCTURE_TYPE_APPLICATION_INFO, .pApplicationName = "ray query in a fragment shader",
        .apiVersion = VK_API_VERSION_1_3 };
    VkInstanceCreateInfo instanceInfo = { .sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO, .pApplicationInfo = &application };
    VkInstance instance;
    CHECK(vkCreateInstance(&instanceInfo, NULL, &instance));
    uint32_t count = 1;
    vkEnumeratePhysicalDevices(instance, &count, &physical);
    VkPhysicalDeviceProperties properties;
    vkGetPhysicalDeviceProperties(physical, &properties);
    printf("device: %s, driver version %u.%u.%u\n", properties.deviceName, VK_VERSION_MAJOR(properties.driverVersion),
        VK_VERSION_MINOR(properties.driverVersion), VK_VERSION_PATCH(properties.driverVersion));

    VkPhysicalDeviceRayQueryFeaturesKHR rayQuery = { .sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_QUERY_FEATURES_KHR, .rayQuery = VK_TRUE };
    VkPhysicalDeviceAccelerationStructureFeaturesKHR structures = { .sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_ACCELERATION_STRUCTURE_FEATURES_KHR,
        .pNext = &rayQuery, .accelerationStructure = VK_TRUE };
    VkPhysicalDeviceVulkan13Features vulkan13 = { .sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES, .pNext = &structures,
        .dynamicRendering = VK_TRUE };
    VkPhysicalDeviceVulkan12Features vulkan12 = { .sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES, .pNext = &vulkan13,
        .bufferDeviceAddress = VK_TRUE };
    const char *extensions[] = { VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME, VK_KHR_RAY_QUERY_EXTENSION_NAME,
        VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME };
    float priority = 1;
    VkDeviceQueueCreateInfo queueInfo = { .sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO, .queueFamilyIndex = 0, .queueCount = 1,
        .pQueuePriorities = &priority };
    VkDeviceCreateInfo deviceInfo = { .sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO, .pNext = &vulkan12, .queueCreateInfoCount = 1,
        .pQueueCreateInfos = &queueInfo, .enabledExtensionCount = 3, .ppEnabledExtensionNames = extensions };
    CHECK(vkCreateDevice(physical, &deviceInfo, NULL, &device));
    VkQueue queue;
    vkGetDeviceQueue(device, 0, 0, &queue);
    createStructure = (PFN_vkCreateAccelerationStructureKHR)vkGetDeviceProcAddr(device, "vkCreateAccelerationStructureKHR");
    buildSizes = (PFN_vkGetAccelerationStructureBuildSizesKHR)vkGetDeviceProcAddr(device, "vkGetAccelerationStructureBuildSizesKHR");
    cmdBuild = (PFN_vkCmdBuildAccelerationStructuresKHR)vkGetDeviceProcAddr(device, "vkCmdBuildAccelerationStructuresKHR");
    structureAddress = (PFN_vkGetAccelerationStructureDeviceAddressKHR)vkGetDeviceProcAddr(device, "vkGetAccelerationStructureDeviceAddressKHR");

    VkCommandPoolCreateInfo poolInfo = { .sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO, .queueFamilyIndex = 0 };
    VkCommandPool pool;
    CHECK(vkCreateCommandPool(device, &poolInfo, NULL, &pool));
    VkCommandBufferAllocateInfo cmdInfo = { .sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO, .commandPool = pool,
        .level = VK_COMMAND_BUFFER_LEVEL_PRIMARY, .commandBufferCount = 1 };
    VkCommandBuffer cmd;
    CHECK(vkAllocateCommandBuffers(device, &cmdInfo, &cmd));
    VkCommandBufferBeginInfo begin = { .sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO };
    CHECK(vkBeginCommandBuffer(cmd, &begin));

    // One triangle in the plane z = 0 over the lower left of the view and its middle, so the rays
    // of pixels along its long edge meet it and their neighbors' miss, then one copy of it.
    Buffer vertices = makeBuffer(sizeof(float) * 9, VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR);
    const float corners[9] = { -1, -1, 0, 1.2f, -1, 0, -1, 1.2f, 0 };
    memcpy(vertices.mapped, corners, sizeof corners);
    VkAccelerationStructureGeometryKHR triangles = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
        .geometryType = VK_GEOMETRY_TYPE_TRIANGLES_KHR, .flags = VK_GEOMETRY_OPAQUE_BIT_KHR };
    triangles.geometry.triangles = (VkAccelerationStructureGeometryTrianglesDataKHR){
        .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR, .vertexFormat = VK_FORMAT_R32G32B32_SFLOAT,
        .vertexData.deviceAddress = vertices.address, .vertexStride = sizeof(float) * 3, .maxVertex = 2, .indexType = VK_INDEX_TYPE_NONE_KHR };
    VkAccelerationStructureKHR bottom = build(cmd, VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR, &triangles, 1);
    structureBarrier(cmd, VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR);

    Buffer instances = makeBuffer(sizeof(VkAccelerationStructureInstanceKHR), VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR);
    VkAccelerationStructureDeviceAddressInfoKHR bottomInfo = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR,
        .accelerationStructure = bottom };
    VkAccelerationStructureInstanceKHR instance0 = { .transform = { .matrix = { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 } } },
        .mask = 0xFF, .flags = VK_GEOMETRY_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT_KHR,
        .accelerationStructureReference = structureAddress(device, &bottomInfo) };
    memcpy(instances.mapped, &instance0, sizeof instance0);
    VkAccelerationStructureGeometryKHR copies = { .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
        .geometryType = VK_GEOMETRY_TYPE_INSTANCES_KHR, .flags = VK_GEOMETRY_OPAQUE_BIT_KHR };
    copies.geometry.instances = (VkAccelerationStructureGeometryInstancesDataKHR){
        .sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR, .data.deviceAddress = instances.address };
    VkAccelerationStructureKHR top = build(cmd, VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR, &copies, 1);
    structureBarrier(cmd, VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT);

    // The image drawn into, and a buffer it is read back through.
    VkImageCreateInfo imageInfo = { .sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO, .imageType = VK_IMAGE_TYPE_2D,
        .format = VK_FORMAT_R8G8B8A8_UNORM, .extent = { Size, Size, 1 }, .mipLevels = 1, .arrayLayers = 1,
        .samples = VK_SAMPLE_COUNT_1_BIT, .tiling = VK_IMAGE_TILING_OPTIMAL,
        .usage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_SRC_BIT };
    VkImage image;
    CHECK(vkCreateImage(device, &imageInfo, NULL, &image));
    VkMemoryRequirements imageNeeds;
    vkGetImageMemoryRequirements(device, image, &imageNeeds);
    VkMemoryAllocateInfo imageMemoryInfo = { .sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO, .allocationSize = imageNeeds.size,
        .memoryTypeIndex = memoryType(imageNeeds.memoryTypeBits, 0) };
    VkDeviceMemory imageMemory;
    CHECK(vkAllocateMemory(device, &imageMemoryInfo, NULL, &imageMemory));
    CHECK(vkBindImageMemory(device, image, imageMemory, 0));
    VkImageViewCreateInfo viewInfo = { .sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO, .image = image,
        .viewType = VK_IMAGE_VIEW_TYPE_2D, .format = VK_FORMAT_R8G8B8A8_UNORM, .subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } };
    VkImageView view;
    CHECK(vkCreateImageView(device, &viewInfo, NULL, &view));
    Buffer readback = makeBuffer(Size * Size * 4, VK_BUFFER_USAGE_TRANSFER_DST_BIT);

    // The scene at the binding asked for in the set asked for, every set before it empty, and
    // beside it at binding 22 a uniform buffer of lights as the engine's model pass reads them: a
    // sun's direction and color, the light from all around, a count, and sixteen lamps.
    VkDescriptorSetLayoutBinding sceneBindings[3] = {
        { .binding = binding, .descriptorType = VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, .descriptorCount = 1,
          .stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT },
        { .binding = 22, .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .descriptorCount = 1,
          .stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT },
        { .binding = 29, .descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, .descriptorCount = 1,
          .stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT } };
    if (binding == 22 || binding == 29) { fprintf(stderr, "bindings 22 and 29 hold the lights and the surfaces\n"); return 1; }
    VkDescriptorSetLayoutCreateInfo emptyInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO };
    VkDescriptorSetLayoutCreateInfo sceneInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO, .bindingCount = 3,
        .pBindings = sceneBindings };
    // And at binding 29 the surfaces the engine's copies are drawn with, each a color, a light it
    // gives off and where its corners begin, 48 bytes, the one copy's green light.
    Buffer surfaces = makeBuffer(4 * 48, VK_BUFFER_USAGE_STORAGE_BUFFER_BIT);
    memset(surfaces.mapped, 0, 4 * 48);
    ((float *)surfaces.mapped)[5] = 1;
    enum { LightsSize = 4 * 16 + 16 * 64 };
    Buffer lights = makeBuffer(LightsSize, VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT);
    float *light = lights.mapped;
    memset(light, 0, LightsSize);
    const float sun[16] = { 0, 0, -1, 1, 0, 1, 0, 1, 0.2f, 0.2f, 0.2f, 0, 1, 0, 0, 0 };
    memcpy(light, sun, sizeof sun);
    const float lamp[16] = { 0, 0, -2, 1, 0, 0, 1, 10, 1, 1, 1, 0, 0, 0, 0, 0 };
    memcpy(light + 16, lamp, sizeof lamp);
    VkDescriptorSetLayout layouts[8];
    if (set >= 8) { fprintf(stderr, "the set is one of 0 to 7\n"); return 1; }
    for (uint32_t i = 0; i <= set; i++) CHECK(vkCreateDescriptorSetLayout(device, i == set ? &sceneInfo : &emptyInfo, NULL, &layouts[i]));
    VkDescriptorPoolSize poolSizes[3] = { { VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, 1 }, { VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, 1 },
        { VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, 1 } };
    VkDescriptorPoolCreateInfo descriptorPoolInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO, .maxSets = 8,
        .poolSizeCount = 3, .pPoolSizes = poolSizes };
    VkDescriptorPool descriptorPool;
    CHECK(vkCreateDescriptorPool(device, &descriptorPoolInfo, NULL, &descriptorPool));
    VkDescriptorSet sets[8];
    VkDescriptorSetAllocateInfo setInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO, .descriptorPool = descriptorPool,
        .descriptorSetCount = set + 1, .pSetLayouts = layouts };
    CHECK(vkAllocateDescriptorSets(device, &setInfo, sets));
    VkWriteDescriptorSetAccelerationStructureKHR sceneWrite = { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR,
        .accelerationStructureCount = 1, .pAccelerationStructures = &top };
    VkDescriptorBufferInfo lightsInfo = { lights.buffer, 0, LightsSize };
    VkDescriptorBufferInfo surfacesInfo = { surfaces.buffer, 0, 4 * 48 };
    VkWriteDescriptorSet writes[3] = {
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .pNext = &sceneWrite, .dstSet = sets[set], .dstBinding = binding,
          .descriptorCount = 1, .descriptorType = VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR },
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .dstSet = sets[set], .dstBinding = 22, .descriptorCount = 1,
          .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .pBufferInfo = &lightsInfo },
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .dstSet = sets[set], .dstBinding = 29, .descriptorCount = 1,
          .descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, .pBufferInfo = &surfacesInfo } };
    vkUpdateDescriptorSets(device, 3, writes, 0, NULL);

    VkPipelineLayoutCreateInfo layoutInfo = { .sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO, .setLayoutCount = set + 1,
        .pSetLayouts = layouts };
    VkPipelineLayout pipelineLayout;
    CHECK(vkCreatePipelineLayout(device, &layoutInfo, NULL, &pipelineLayout));
    VkPipelineShaderStageCreateInfo stages[2] = {
        { .sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO, .stage = VK_SHADER_STAGE_VERTEX_BIT,
          .module = loadShader("vert.spv"), .pName = "main" },
        { .sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO, .stage = VK_SHADER_STAGE_FRAGMENT_BIT,
          .module = loadShader("frag.spv"), .pName = "main" } };
    VkPipelineVertexInputStateCreateInfo input = { .sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO };
    VkPipelineInputAssemblyStateCreateInfo assembly = { .sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
        .topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST };
    VkViewport viewport = { 0, 0, Size, Size, 0, 1 };
    VkRect2D scissor = { { 0, 0 }, { Size, Size } };
    VkPipelineViewportStateCreateInfo viewportState = { .sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
        .viewportCount = 1, .pViewports = &viewport, .scissorCount = 1, .pScissors = &scissor };
    VkPipelineRasterizationStateCreateInfo raster = { .sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
        .polygonMode = VK_POLYGON_MODE_FILL, .cullMode = VK_CULL_MODE_NONE, .lineWidth = 1 };
    VkPipelineMultisampleStateCreateInfo multisample = { .sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
        .rasterizationSamples = VK_SAMPLE_COUNT_1_BIT };
    VkPipelineColorBlendAttachmentState blendAttachment = { .colorWriteMask = 0xF };
    VkPipelineColorBlendStateCreateInfo blend = { .sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
        .attachmentCount = 1, .pAttachments = &blendAttachment };
    VkFormat format = VK_FORMAT_R8G8B8A8_UNORM;
    VkPipelineRenderingCreateInfo rendering = { .sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO,
        .colorAttachmentCount = 1, .pColorAttachmentFormats = &format };
    VkGraphicsPipelineCreateInfo pipelineInfo = { .sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO, .pNext = &rendering,
        .stageCount = 2, .pStages = stages, .pVertexInputState = &input, .pInputAssemblyState = &assembly,
        .pViewportState = &viewportState, .pRasterizationState = &raster, .pMultisampleState = &multisample,
        .pColorBlendState = &blend, .layout = pipelineLayout };
    VkPipeline pipeline;
    CHECK(vkCreateGraphicsPipelines(device, VK_NULL_HANDLE, 1, &pipelineInfo, NULL, &pipeline));

    imageBarrier(cmd, image, VK_IMAGE_LAYOUT_UNDEFINED, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, 0,
        VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT);
    VkRenderingAttachmentInfo color = { .sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO, .imageView = view,
        .imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, .loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR,
        .storeOp = VK_ATTACHMENT_STORE_OP_STORE, .clearValue.color.float32 = { 0, 0, 0, 1 } };
    VkRenderingInfo renderingInfo = { .sType = VK_STRUCTURE_TYPE_RENDERING_INFO, .renderArea = scissor, .layerCount = 1,
        .colorAttachmentCount = 1, .pColorAttachments = &color };
    vkCmdBeginRendering(cmd, &renderingInfo);
    vkCmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, pipeline);
    vkCmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, pipelineLayout, 0, set + 1, sets, 0, NULL);
    vkCmdDraw(cmd, 3, 1, 0, 0);
    vkCmdEndRendering(cmd);
    imageBarrier(cmd, image, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
        VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT, VK_ACCESS_TRANSFER_READ_BIT, VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT,
        VK_PIPELINE_STAGE_TRANSFER_BIT);
    VkBufferImageCopy region = { .imageSubresource = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, .imageExtent = { Size, Size, 1 } };
    vkCmdCopyImageToBuffer(cmd, image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, readback.buffer, 1, &region);
    CHECK(vkEndCommandBuffer(cmd));

    VkSubmitInfo submit = { .sType = VK_STRUCTURE_TYPE_SUBMIT_INFO, .commandBufferCount = 1, .pCommandBuffers = &cmd };
    CHECK(vkQueueSubmit(queue, 1, &submit, VK_NULL_HANDLE));
    CHECK(vkQueueWaitIdle(queue));

    const unsigned char *pixel = (const unsigned char *)readback.mapped + (Size / 2 * Size + Size / 2) * 4;
    printf("set %u, binding %u: the middle pixel is %u %u %u, %s\n", set, binding, pixel[0], pixel[1], pixel[2],
        pixel[0] == 255 && pixel[2] == 0 ? "the ray met the triangle" : "the ray met nothing");
    return pixel[0] == 255 && pixel[2] == 0 ? 0 : 2;
}
