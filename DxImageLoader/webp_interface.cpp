#include "pch.h"
#include "webp_interface.h"
#include "interface.h"
#include <webp/decode.h>
#include <webp/encode.h>
#include <webp/demux.h>
#include <webp/mux.h>
#include <fstream>
#include <vector>
#include <stdexcept>

class WebpImage : public image::IImage
{
public:
    WebpImage(const char* filename)
    {
        // Read file into buffer
        std::ifstream file(filename, std::ios::binary | std::ios::ate);
        if (!file)
            throw std::runtime_error("could not open file");
        std::streamsize size = file.tellg();
        file.seekg(0, std::ios::beg);
        m_buffer.resize(size);
        if (!file.read(reinterpret_cast<char*>(m_buffer.data()), size))
            throw std::runtime_error("could not read file");
        file.close();

        // Get original format
        WebPBitstreamFeatures features;
        if (WebPGetFeatures(m_buffer.data(), m_buffer.size(), &features) != VP8_STATUS_OK)
            throw std::runtime_error("WebPGetFeatures failed");

        m_hasAlpha = features.has_alpha != 0;
        m_originalFormat = m_hasAlpha ? gli::format::FORMAT_RGBA8_SRGB_PACK8 : gli::format::FORMAT_RGB8_SRGB_PACK8;

        // Demux for animation
        WebPData webp_data;
        webp_data.bytes = m_buffer.data();
        webp_data.size = m_buffer.size();
        WebPDemuxer* demux = WebPDemux(&webp_data);
        if (!demux)
            throw std::runtime_error("WebPDemux failed");

        m_frameCount = WebPDemuxGetI(demux, WEBP_FF_FRAME_COUNT);
        m_width = WebPDemuxGetI(demux, WEBP_FF_CANVAS_WIDTH);
        m_height = WebPDemuxGetI(demux, WEBP_FF_CANVAS_HEIGHT);

        m_frames.reserve(m_frameCount);
        std::vector<uint8_t> prevFrame(m_width * m_height * 4, 0);

        WebPIterator iter;
        if (!WebPDemuxGetFrame(demux, 1, &iter))
            throw std::runtime_error("WebPDemuxGetFrame failed");

        size_t totalDurationMs = 0;

        do {
            int decodeWidth = 0, decodeHeight = 0;
            uint8_t* decoded = WebPDecodeRGBA(iter.fragment.bytes, iter.fragment.size, &decodeWidth, &decodeHeight);
            if (!decoded)
                throw std::runtime_error("WebP frame decode failed");

            totalDurationMs += size_t(iter.duration);

            // Start with previous frame (or clear for first frame)
            std::vector<uint8_t> frame(prevFrame);

            // Composite the decoded region into the frame at the correct offset
			auto frameIndex = m_frames.size();
            for (int y = 0; y < decodeHeight; ++y) {
                set_progress((uint32_t)((frameIndex * m_height + y) * 100) / (m_frameCount * m_height));
                
                int destY = iter.y_offset + y;
                if (destY < 0 || destY >= int(m_height)) continue;
                for (int x = 0; x < decodeWidth; ++x) {
                    int destX = iter.x_offset + x;
                    if (destX < 0 || destX >= int(m_width)) continue;
                    size_t dstIdx = (destY * m_width + destX) * 4;
                    size_t srcIdx = (y * decodeWidth + x) * 4;

                    if (iter.blend_method == WEBP_MUX_BLEND && decoded[srcIdx + 3] < 255) {
                        // Alpha blend with previous frame
                        float srcA = decoded[srcIdx + 3] / 255.0f;
                        float dstA = frame[dstIdx + 3] / 255.0f;
                        float outA = srcA + dstA * (1 - srcA);
                        for (int c = 0; c < 4; ++c) {
                            float srcC = decoded[srcIdx + c] / 255.0f;
                            float dstC = frame[dstIdx + c] / 255.0f;
                            float outC = (srcC * srcA + dstC * dstA * (1 - srcA)) / (outA > 0 ? outA : 1);
                            frame[dstIdx + c] = static_cast<uint8_t>(outC * 255.0f + 0.5f);
                        }
                    }
                    else {
                        // No blend, just copy
                        std::memcpy(&frame[dstIdx], &decoded[srcIdx], 4);
                    }
                }
            }

            // Handle dispose method
            if (iter.dispose_method == WEBP_MUX_DISPOSE_BACKGROUND) {
                // If the next frame needs a cleared region, clear it in prevFrame
                for (int y = 0; y < decodeHeight; ++y) {
                    int destY = iter.y_offset + y;
                    if (destY < 0 || destY >= int(m_height)) continue;
                    for (int x = 0; x < decodeWidth; ++x) {
                        int destX = iter.x_offset + x;
                        if (destX < 0 || destX >= int(m_width)) continue;
                        size_t dstIdx = (destY * m_width + destX) * 4;
                        prevFrame[dstIdx + 0] = 0;
                        prevFrame[dstIdx + 1] = 0;
                        prevFrame[dstIdx + 2] = 0;
                        prevFrame[dstIdx + 3] = 0;
                    }
                }
            }
            else {
                // Otherwise, next frame starts from this one
                prevFrame = frame;
            }

            m_frames.push_back(std::move(frame));
            WebPFree(decoded);
        } while (WebPDemuxNextFrame(&iter));

        WebPDemuxReleaseIterator(&iter);
        WebPDemuxDelete(demux);

        if (totalDurationMs > 0)
            m_fps = (1000.0f * float(m_frameCount)) / float(totalDurationMs);
    }

    ~WebpImage() override = default;

    uint32_t getNumLayers() const override { return static_cast<uint32_t>(m_frames.size()); }
    uint32_t getNumMipmaps() const override { return 1; }
    uint32_t getWidth(uint32_t /*mipmap*/) const override { return m_width; }
    uint32_t getHeight(uint32_t /*mipmap*/) const override { return m_height; }
    uint32_t getDepth(uint32_t /*mipmap*/) const override { return 1; }
    gli::format getFormat() const override { return gli::format::FORMAT_RGBA8_SRGB_PACK8; }
    gli::format getOriginalFormat() const override { return m_originalFormat; }

    uint8_t* getData(uint32_t layer, uint32_t mipmap, size_t& size) override {
        size = m_width * m_height * 4;
        return m_frames[layer].data();
    }
    const uint8_t* getData(uint32_t layer, uint32_t mipmap, size_t& size) const override {
        size = m_width * m_height * 4;
        return m_frames[layer].data();
    }

    virtual float getFps() const override {
        return m_fps;
	}

private:
    std::vector<uint8_t> m_buffer;
    std::vector<std::vector<uint8_t>> m_frames;
    uint32_t m_width = 0, m_height = 0, m_frameCount = 0;
    bool m_hasAlpha = false;
    gli::format m_originalFormat = gli::format::FORMAT_RGBA8_SRGB_PACK8;
    float m_fps = 0.0f;
};

std::unique_ptr<image::IImage> webp_load(const char* filename)
{
    return std::make_unique<WebpImage>(filename);
}

std::vector<uint32_t> webp_get_export_formats()
{
    return std::vector<uint32_t>{
        // gli::format::FORMAT_RGB8_SRGB_PACK8,
		gli::format::FORMAT_RGBA8_SRGB_PACK8, // only this for simplicity
    };
}

void webp_save_image(const char* filename, image::IImage& image, gli::format format, int quality, float fps)
{
    const uint32_t numLayers = image.getNumLayers();
    const uint32_t width = image.getWidth(0);
    const uint32_t height = image.getHeight(0);

    image.applyBGRPostprocess(); // WebP expect BGRA order (argb)

    std::vector<uint8_t> webp_data; // For static image

    if (fps < 0.0f) fps = 24.0f; // default to 24 fps 
	float msFrame = 1000 / fps;
    WebPAnimEncoderOptions enc_opts = {};
	enc_opts.minimize_size = 1;
    enc_opts.allow_mixed = 1;
    enc_opts.kmin = 1;
	enc_opts.kmax = 2048;
    auto ret = WebPAnimEncoderOptionsInit(&enc_opts);
    assert(ret);

	WebPConfig config = {};
	config.lossless = quality >= 100 ? 1 : 0;
	config.quality = float(quality);
	config.method = 6; // slowest but best compression
	config.segments = 4;
    config.alpha_compression = 1;
	config.alpha_quality = quality;
    config.pass = 10;
    config.near_lossless = config.lossless ? 100 : 0;
    config.partition_limit = 0; // best quality
    config.sns_strength = 50; // default
    config.filter_strength = 60; // default
    config.filter_sharpness = 0; // default
    config.alpha_filtering = 1; // default
    config.exact = 0; // default
    config.use_sharp_yuv = 0; // default
    config.qmin = 0; config.qmax = 100;

    WebPAnimEncoder* enc = WebPAnimEncoderNew(width, height, &enc_opts);

    for (uint32_t layer = 0; layer < image.getNumLayers(); ++layer) 
    {
        WebPPicture pic = {};
        ret = WebPPictureInit(&pic);
        assert(ret);
        pic.width = width;
        pic.height = height;
        pic.use_argb = 1;
		size_t dataSize;
        pic.argb = reinterpret_cast<uint32_t*>(image.getData(layer, 0, dataSize));
        pic.argb_stride = width;
        std::pair<uint32_t, uint32_t> curLayer = { layer, image.getNumLayers() };
        pic.user_data = &curLayer;
		pic.progress_hook = [](int percent, const WebPPicture* pic) -> int {
            auto curLayer = *reinterpret_cast<std::pair<uint32_t, uint32_t>*>(pic->user_data);
            set_progress((curLayer.first * 100 + percent) / curLayer.second);
			return 1; // return false to abort encoding
		};

        // timestamp_ms = cumulative display duration
        ret = WebPAnimEncoderAdd(enc, &pic, int(msFrame * float(layer)), &config);
        assert(ret);
        WebPPictureFree(&pic);
    }

    WebPData out_data;
    WebPDataInit(&out_data);
    ret = WebPAnimEncoderAssemble(enc, &out_data);
    assert(ret);
    WebPAnimEncoderDelete(enc);

    std::ofstream out(filename, std::ios::binary);
    out.write(reinterpret_cast<const char*>(out_data.bytes), out_data.size);
    out.close();
}
