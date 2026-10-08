// Dispatches comp.spv, a compute shader of 64 invocations a group, over four groups, with a uniform
// buffer of lights at binding 4 of set 0, as 3DEngine's light bounce reads them, and a 3D storage
// image of half floats at binding 5, to report a crash of lavapipe's. A uniform buffer at binding 1
// and 32 bytes of push constants, which the bounce's pass has and the shader does not read, are
// kept as the pass lays them out.
//
//   cc comp.c -o comp -lvulkan && ./comp
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <vulkan/vulkan.h>

#define CHECK(call) do { VkResult result_ = (call); if (result_ != VK_SUCCESS) { \
    fprintf(stderr, "%s failed with %d at line %d\n", #call, result_, __LINE__); exit(1); } } while (0)

static VkPhysicalDevice physical;
static VkDevice device;

static uint32_t memoryType(uint32_t bits, VkMemoryPropertyFlags wanted)
{
    VkPhysicalDeviceMemoryProperties properties;
    vkGetPhysicalDeviceMemoryProperties(physical, &properties);
    for (uint32_t i = 0; i < properties.memoryTypeCount; i++)
        if ((bits & (1u << i)) && (properties.memoryTypes[i].propertyFlags & wanted) == wanted) return i;
    fprintf(stderr, "no memory type fits\n");
    exit(1);
}

typedef struct { VkBuffer buffer; VkDeviceMemory memory; void *mapped; } Buffer;

static Buffer makeBuffer(VkDeviceSize size, VkBufferUsageFlags usage)
{
    Buffer made = {0};
    VkBufferCreateInfo info = { .sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO, .size = size, .usage = usage };
    CHECK(vkCreateBuffer(device, &info, NULL, &made.buffer));
    VkMemoryRequirements needs;
    vkGetBufferMemoryRequirements(device, made.buffer, &needs);
    VkMemoryAllocateInfo allocate = { .sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO, .allocationSize = needs.size,
        .memoryTypeIndex = memoryType(needs.memoryTypeBits, VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) };
    CHECK(vkAllocateMemory(device, &allocate, NULL, &made.memory));
    CHECK(vkBindBufferMemory(device, made.buffer, made.memory, 0));
    CHECK(vkMapMemory(device, made.memory, 0, VK_WHOLE_SIZE, 0, &made.mapped));
    memset(made.mapped, 0, size);
    return made;
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

int main(void)
{
    VkApplicationInfo application = { .sType = VK_STRUCTURE_TYPE_APPLICATION_INFO, .pApplicationName = "loop over a uniform buffer",
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
    fflush(stdout);
    float priority = 1;
    VkDeviceQueueCreateInfo queueInfo = { .sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO, .queueFamilyIndex = 0, .queueCount = 1,
        .pQueuePriorities = &priority };
    VkDeviceCreateInfo deviceInfo = { .sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO, .queueCreateInfoCount = 1, .pQueueCreateInfos = &queueInfo };
    CHECK(vkCreateDevice(physical, &deviceInfo, NULL, &device));
    VkQueue queue;
    vkGetDeviceQueue(device, 0, 0, &queue);

    // The lights as the bounce reads them: a count in the first vector's x, then sixteen lamps of
    // three vectors each, the count one, so a loop over them that starts past it never runs.
    enum { LightsSize = 16 + 16 * 48 };
    Buffer lights = makeBuffer(LightsSize, VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT);
    ((float *)lights.mapped)[0] = 1;
    Buffer other = makeBuffer(256, VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT);

    VkImageCreateInfo imageInfo = { .sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO, .imageType = VK_IMAGE_TYPE_3D,
        .format = VK_FORMAT_R16G16B16A16_SFLOAT, .extent = { 16, 16, 16 }, .mipLevels = 1, .arrayLayers = 1,
        .samples = VK_SAMPLE_COUNT_1_BIT, .tiling = VK_IMAGE_TILING_OPTIMAL, .usage = VK_IMAGE_USAGE_STORAGE_BIT };
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
        .viewType = VK_IMAGE_VIEW_TYPE_3D, .format = VK_FORMAT_R16G16B16A16_SFLOAT, .subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } };
    VkImageView view;
    CHECK(vkCreateImageView(device, &viewInfo, NULL, &view));

    VkDescriptorSetLayoutBinding bindings[3] = {
        { .binding = 1, .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .descriptorCount = 1, .stageFlags = VK_SHADER_STAGE_COMPUTE_BIT },
        { .binding = 4, .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .descriptorCount = 1, .stageFlags = VK_SHADER_STAGE_COMPUTE_BIT },
        { .binding = 5, .descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, .descriptorCount = 1, .stageFlags = VK_SHADER_STAGE_COMPUTE_BIT } };
    VkDescriptorSetLayoutCreateInfo setLayoutInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO, .bindingCount = 3,
        .pBindings = bindings };
    VkDescriptorSetLayout setLayout;
    CHECK(vkCreateDescriptorSetLayout(device, &setLayoutInfo, NULL, &setLayout));
    VkDescriptorPoolSize poolSizes[2] = { { VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, 2 }, { VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, 1 } };
    VkDescriptorPoolCreateInfo poolInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO, .maxSets = 1, .poolSizeCount = 2,
        .pPoolSizes = poolSizes };
    VkDescriptorPool pool;
    CHECK(vkCreateDescriptorPool(device, &poolInfo, NULL, &pool));
    VkDescriptorSetAllocateInfo setInfo = { .sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO, .descriptorPool = pool,
        .descriptorSetCount = 1, .pSetLayouts = &setLayout };
    VkDescriptorSet set;
    CHECK(vkAllocateDescriptorSets(device, &setInfo, &set));
    VkDescriptorBufferInfo otherInfo = { other.buffer, 0, 256 }, lightsInfo = { lights.buffer, 0, LightsSize };
    VkDescriptorImageInfo storageInfo = { VK_NULL_HANDLE, view, VK_IMAGE_LAYOUT_GENERAL };
    VkWriteDescriptorSet writes[3] = {
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .dstSet = set, .dstBinding = 1, .descriptorCount = 1,
          .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .pBufferInfo = &otherInfo },
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .dstSet = set, .dstBinding = 4, .descriptorCount = 1,
          .descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, .pBufferInfo = &lightsInfo },
        { .sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, .dstSet = set, .dstBinding = 5, .descriptorCount = 1,
          .descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, .pImageInfo = &storageInfo } };
    vkUpdateDescriptorSets(device, 3, writes, 0, NULL);

    VkPushConstantRange push = { VK_SHADER_STAGE_COMPUTE_BIT, 0, 32 };
    VkPipelineLayoutCreateInfo layoutInfo = { .sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO, .setLayoutCount = 1,
        .pSetLayouts = &setLayout, .pushConstantRangeCount = 1, .pPushConstantRanges = &push };
    VkPipelineLayout pipelineLayout;
    CHECK(vkCreatePipelineLayout(device, &layoutInfo, NULL, &pipelineLayout));
    VkComputePipelineCreateInfo pipelineInfo = { .sType = VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO,
        .stage = { .sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO, .stage = VK_SHADER_STAGE_COMPUTE_BIT,
            .module = loadShader("comp.spv"), .pName = "main" }, .layout = pipelineLayout };
    VkPipeline pipeline;
    CHECK(vkCreateComputePipelines(device, VK_NULL_HANDLE, 1, &pipelineInfo, NULL, &pipeline));

    VkCommandPoolCreateInfo commandPoolInfo = { .sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO, .queueFamilyIndex = 0 };
    VkCommandPool commandPool;
    CHECK(vkCreateCommandPool(device, &commandPoolInfo, NULL, &commandPool));
    VkCommandBufferAllocateInfo cmdInfo = { .sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO, .commandPool = commandPool,
        .level = VK_COMMAND_BUFFER_LEVEL_PRIMARY, .commandBufferCount = 1 };
    VkCommandBuffer cmd;
    CHECK(vkAllocateCommandBuffers(device, &cmdInfo, &cmd));
    VkCommandBufferBeginInfo begin = { .sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO };
    CHECK(vkBeginCommandBuffer(cmd, &begin));
    VkImageMemoryBarrier toGeneral = { .sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER, .dstAccessMask = VK_ACCESS_SHADER_WRITE_BIT,
        .oldLayout = VK_IMAGE_LAYOUT_UNDEFINED, .newLayout = VK_IMAGE_LAYOUT_GENERAL, .srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
        .dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED, .image = image, .subresourceRange = { VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 } };
    vkCmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT, VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, 0, 0, NULL, 0, NULL, 1, &toGeneral);
    vkCmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_COMPUTE, pipeline);
    vkCmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_COMPUTE, pipelineLayout, 0, 1, &set, 0, NULL);
    const uint32_t pushed[8] = { 0, 4, 4, 2, 0, 0, 0, 0 };
    vkCmdPushConstants(cmd, pipelineLayout, VK_SHADER_STAGE_COMPUTE_BIT, 0, sizeof pushed, pushed);
    vkCmdDispatch(cmd, 4, 1, 1);
    CHECK(vkEndCommandBuffer(cmd));
    VkSubmitInfo submit = { .sType = VK_STRUCTURE_TYPE_SUBMIT_INFO, .commandBufferCount = 1, .pCommandBuffers = &cmd };
    CHECK(vkQueueSubmit(queue, 1, &submit, VK_NULL_HANDLE));
    CHECK(vkQueueWaitIdle(queue));
    printf("dispatched\n");
    return 0;
}
