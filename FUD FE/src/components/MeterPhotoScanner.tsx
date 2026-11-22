import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { ewApi, type MeterPhotoScanResponseDto } from '../services/api';
import { compressMeterImage } from '../utils/imageCompression';
import { ImageLightboxModal } from './ImageLightboxModal';

export interface MeterPhotoScannerProps {
  expectedMeterType: 'Electricity' | 'Water';
  previousValue?: number;
  saveEvidence?: boolean;
  compact?: boolean;
  disabled?: boolean;
  label?: string;
  existingImageUrl?: string;
  onScanned: (result: MeterPhotoScanResponseDto, localPreviewUrl?: string) => void;
  onError?: (message: string) => void;
}

const CameraIcon: React.FC<{ size?: number }> = ({ size = 14 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
    <circle cx="12" cy="13" r="4" />
  </svg>
);

export const MeterPhotoScanner: React.FC<MeterPhotoScannerProps> = ({
  expectedMeterType,
  previousValue,
  saveEvidence = true,
  compact = false,
  disabled = false,
  label,
  existingImageUrl,
  onScanned,
  onError,
}) => {
  const mobileCameraInputRef = useRef<HTMLInputElement>(null);
  const filePickerInputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const previewUrlRef = useRef<string | null>(null);

  const [scanning, setScanning] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [scanResult, setScanResult] = useState<MeterPhotoScanResponseDto | null>(null);
  const [localPreviewUrl, setLocalPreviewUrl] = useState<string | null>(null);
  const [webcamOpen, setWebcamOpen] = useState(false);
  const [webcamError, setWebcamError] = useState<string | null>(null);
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);

  const supportsWebcam =
    typeof navigator !== 'undefined' &&
    typeof navigator.mediaDevices !== 'undefined' &&
    typeof navigator.mediaDevices.getUserMedia === 'function';

  const stopWebcamStream = useCallback(() => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
  }, []);

  const closeWebcam = useCallback(() => {
    stopWebcamStream();
    setWebcamOpen(false);
    setWebcamError(null);
  }, [stopWebcamStream]);

  // Cleanup webcam stream & object URL on unmount
  useEffect(() => {
    return () => {
      stopWebcamStream();
      if (previewUrlRef.current) {
        URL.revokeObjectURL(previewUrlRef.current);
        previewUrlRef.current = null;
      }
    };
  }, [stopWebcamStream]);

  // Attach active MediaStream to video element when modal opens
  useEffect(() => {
    if (!webcamOpen || !supportsWebcam) return;

    let cancelled = false;
    navigator.mediaDevices
      .getUserMedia({
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      })
      .then((stream) => {
        if (cancelled) {
          stream.getTracks().forEach((track) => track.stop());
          return;
        }
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          videoRef.current.play().catch(() => {});
        }
      })
      .catch(() => {
        if (!cancelled) {
          setWebcamError('Không thể mở camera. Vui lòng cấp quyền camera hoặc dùng nút "Chọn ảnh".');
        }
      });

    return () => {
      cancelled = true;
      stopWebcamStream();
    };
  }, [webcamOpen, supportsWebcam, stopWebcamStream]);

  // Intercept Escape in capture phase so closing webcam does not close parent modals
  useEffect(() => {
    if (!webcamOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        e.stopImmediatePropagation();
        closeWebcam();
      }
    };

    window.addEventListener('keydown', handleKeyDown, true);
    return () => window.removeEventListener('keydown', handleKeyDown, true);
  }, [webcamOpen, closeWebcam]);

  const replaceLocalPreview = useCallback((file: File): string => {
    if (previewUrlRef.current) {
      URL.revokeObjectURL(previewUrlRef.current);
    }
    const nextUrl = URL.createObjectURL(file);
    previewUrlRef.current = nextUrl;
    setLocalPreviewUrl(nextUrl);
    return nextUrl;
  }, []);

  const processFile = useCallback(
    async (rawFile: File) => {
      setScanning(true);
      setErrorMsg(null);
      try {
        const compressed = await compressMeterImage(rawFile, 1600, 0.85);
        const previewUrl = replaceLocalPreview(compressed);
        const result = await ewApi.scanMeterPhoto(compressed, {
          expectedMeterType,
          previousValue,
          saveEvidence,
        });
        setScanResult(result);
        onScanned(result, previewUrl);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : 'Lỗi khi nhận diện ảnh công tơ';
        setErrorMsg(msg);
        onError?.(msg);
      } finally {
        setScanning(false);
      }
    },
    [expectedMeterType, previousValue, saveEvidence, onScanned, onError, replaceLocalPreview]
  );

  const handleFileSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    await processFile(file);
  };

  const handleCaptureFromWebcam = async () => {
    const video = videoRef.current;
    if (!video || video.videoWidth === 0 || video.videoHeight === 0) {
      setWebcamError('Camera chưa sẵn sàng, vui lòng chờ thêm 1 giây.');
      return;
    }

    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) => {
      canvas.toBlob((res) => resolve(res), 'image/jpeg', 0.9);
    });

    closeWebcam();
    if (!blob) return;

    const capturedFile = new File([blob], `meter-capture-${Date.now()}.jpg`, {
      type: 'image/jpeg',
    });
    await processFile(capturedFile);
  };

  const displayImageUrl = scanResult?.evidenceImageUrl || localPreviewUrl || existingImageUrl || null;
  const meterLabel = expectedMeterType === 'Electricity' ? 'Điện' : 'Nước';

  const confidenceBadge = (() => {
    if (!scanResult) return null;
    const pct = Math.round(scanResult.confidence * 100);
    if (scanResult.confidenceLevel === 'High') {
      return { text: `AI ${pct}%`, bg: '#DCFCE7', color: '#15803D', border: '#86EFAC' };
    }
    if (scanResult.confidenceLevel === 'Medium') {
      return { text: `AI ${pct}%`, bg: '#FEF9C3', color: '#B45309', border: '#FDE047' };
    }
    return { text: `AI ${pct}%`, bg: '#FEE2E2', color: '#DC2626', border: '#FCA5A5' };
  })();

  const webcamModal =
    webcamOpen && typeof document !== 'undefined'
      ? createPortal(
          <div
            onClick={closeWebcam}
            style={{
              position: 'fixed',
              inset: 0,
              zIndex: 10050,
              backgroundColor: 'rgba(15, 23, 42, 0.78)',
              backdropFilter: 'blur(3px)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              padding: 16,
            }}
          >
            <div
              onClick={(e) => e.stopPropagation()}
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: 14,
                width: '100%',
                maxWidth: 560,
                overflow: 'hidden',
                boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.45)',
              }}
            >
              <div
                style={{
                  padding: '14px 18px',
                  borderBottom: '1px solid #E2E8F0',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <div>
                  <div style={{ fontWeight: 700, fontSize: 15, color: '#0F172A' }}>
                    Chụp ảnh công tơ {meterLabel} {label ? `— ${label}` : ''}
                  </div>
                  <div style={{ fontSize: 12, color: '#64748B' }}>
                    Căn chỉnh dãy số công tơ nằm gọn trong khung màu cam
                  </div>
                </div>
                <button
                  type="button"
                  onClick={closeWebcam}
                  style={{
                    width: 30,
                    height: 30,
                    borderRadius: '50%',
                    border: 'none',
                    background: '#F1F5F9',
                    color: '#475569',
                    cursor: 'pointer',
                    fontSize: 15,
                  }}
                >
                  ✕
                </button>
              </div>

              <div style={{ padding: 16, background: '#0F172A', position: 'relative' }}>
                {webcamError ? (
                  <div
                    style={{
                      padding: 24,
                      textAlign: 'center',
                      color: '#FCA5A5',
                      fontSize: 13,
                    }}
                  >
                    {webcamError}
                  </div>
                ) : (
                  <div style={{ position: 'relative', borderRadius: 10, overflow: 'hidden' }}>
                    <video
                      ref={videoRef}
                      autoPlay
                      playsInline
                      muted
                      style={{ width: '100%', maxHeight: 360, objectFit: 'cover', display: 'block' }}
                    />
                    <div
                      style={{
                        position: 'absolute',
                        left: '12%',
                        right: '12%',
                        top: '32%',
                        bottom: '32%',
                        border: '2px dashed #FB923C',
                        borderRadius: 8,
                        boxShadow: '0 0 0 9999px rgba(15, 23, 42, 0.32)',
                        pointerEvents: 'none',
                        display: 'flex',
                        alignItems: 'flex-end',
                        justifyContent: 'center',
                        paddingBottom: 6,
                      }}
                    >
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          color: '#FFEDD5',
                          background: 'rgba(15, 23, 42, 0.72)',
                          padding: '2px 8px',
                          borderRadius: 4,
                        }}
                      >
                        Căn chỉnh dãy số công tơ vào khung
                      </span>
                    </div>
                  </div>
                )}
              </div>

              <div
                style={{
                  padding: '12px 18px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: 10,
                  borderTop: '1px solid #E2E8F0',
                }}
              >
                <button
                  type="button"
                  onClick={() => {
                    closeWebcam();
                    filePickerInputRef.current?.click();
                  }}
                  style={{
                    padding: '8px 14px',
                    borderRadius: 8,
                    border: '1px solid #CBD5E1',
                    background: '#F8FAFC',
                    color: '#334155',
                    fontSize: 13,
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  Chọn ảnh từ máy
                </button>
                <div style={{ display: 'flex', gap: 8 }}>
                  <button
                    type="button"
                    onClick={closeWebcam}
                    style={{
                      padding: '8px 14px',
                      borderRadius: 8,
                      border: '1px solid #CBD5E1',
                      background: '#FFFFFF',
                      color: '#64748B',
                      fontSize: 13,
                      cursor: 'pointer',
                    }}
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    disabled={!!webcamError}
                    onClick={handleCaptureFromWebcam}
                    style={{
                      padding: '8px 18px',
                      borderRadius: 8,
                      border: 'none',
                      background: webcamError ? '#94A3B8' : '#D97026',
                      color: '#FFFFFF',
                      fontSize: 13,
                      fontWeight: 700,
                      cursor: webcamError ? 'not-allowed' : 'pointer',
                    }}
                  >
                    Chụp &amp; Quét AI
                  </button>
                </div>
              </div>
            </div>
          </div>,
          document.body
        )
      : null;

  if (compact) {
    return (
      <span className="no-print" style={{ display: 'inline-flex', alignItems: 'center', flexShrink: 0 }}>
        <input
          ref={mobileCameraInputRef}
          type="file"
          accept="image/*"
          capture="environment"
          onChange={handleFileSelected}
          style={{ display: 'none' }}
        />
        <input
          ref={filePickerInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          onChange={handleFileSelected}
          style={{ display: 'none' }}
        />
        <button
          type="button"
          className="no-print"
          disabled={disabled || scanning}
          title={
            errorMsg
              ? `Lỗi: ${errorMsg}`
              : scanResult?.readingValue !== undefined
                ? `AI đọc: ${scanResult.readingValue} (${Math.round(scanResult.confidence * 100)}%) — Bấm để chụp lại`
                : `Chụp ảnh công tơ ${meterLabel} bằng AI`
          }
          onClick={() => {
            if (supportsWebcam) {
              setWebcamError(null);
              setWebcamOpen(true);
            } else {
              mobileCameraInputRef.current?.click();
            }
          }}
          style={{
            width: 22,
            height: 22,
            padding: 0,
            borderRadius: 4,
            border: errorMsg ? '1px solid #EF4444' : '1px solid #CBD5E1',
            background: scanning ? '#FEF3C7' : scanResult?.readingValue !== undefined ? '#ECFDF5' : '#F8FAFC',
            color: errorMsg ? '#DC2626' : scanResult?.readingValue !== undefined ? '#059669' : '#D97026',
            cursor: disabled || scanning ? 'not-allowed' : 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 11,
            fontWeight: 700,
            flexShrink: 0,
          }}
        >
          {scanning ? '…' : <CameraIcon size={12} />}
        </button>
        {webcamModal}
      </span>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <input
        ref={mobileCameraInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        onChange={handleFileSelected}
        style={{ display: 'none' }}
      />
      <input
        ref={filePickerInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        onChange={handleFileSelected}
        style={{ display: 'none' }}
      />

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center' }}>
        <button
          type="button"
          disabled={disabled || scanning}
          onClick={() => mobileCameraInputRef.current?.click()}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 6,
            padding: '6px 11px',
            borderRadius: 7,
            border: 'none',
            background: disabled || scanning ? '#94A3B8' : '#D97026',
            color: '#FFFFFF',
            fontSize: 12,
            fontWeight: 600,
            cursor: disabled || scanning ? 'not-allowed' : 'pointer',
          }}
        >
          <CameraIcon size={13} />
          {scanning ? 'AI đang đọc chỉ số...' : displayImageUrl ? 'Chụp lại' : 'Chụp ảnh'}
        </button>

        {supportsWebcam && (
          <button
            type="button"
            disabled={disabled || scanning}
            onClick={() => {
              setWebcamError(null);
              setWebcamOpen(true);
            }}
            style={{
              padding: '6px 10px',
              borderRadius: 7,
              border: '1px solid #CBD5E1',
              background: '#FFFFFF',
              color: '#334155',
              fontSize: 12,
              fontWeight: 600,
              cursor: disabled || scanning ? 'not-allowed' : 'pointer',
            }}
          >
            Mở Camera
          </button>
        )}

        <button
          type="button"
          disabled={disabled || scanning}
          onClick={() => filePickerInputRef.current?.click()}
          style={{
            padding: '6px 10px',
            borderRadius: 7,
            border: '1px solid #CBD5E1',
            background: '#F8FAFC',
            color: '#334155',
            fontSize: 12,
            fontWeight: 600,
            cursor: disabled || scanning ? 'not-allowed' : 'pointer',
          }}
        >
          Chọn ảnh
        </button>

        {confidenceBadge && (
          <span
            style={{
              padding: '3px 8px',
              borderRadius: 12,
              fontSize: 11,
              fontWeight: 700,
              background: confidenceBadge.bg,
              color: confidenceBadge.color,
              border: `1px solid ${confidenceBadge.border}`,
            }}
          >
            {confidenceBadge.text}
          </span>
        )}
      </div>

      {errorMsg && (
        <div
          style={{
            padding: '6px 10px',
            borderRadius: 6,
            background: '#FEF2F2',
            border: '1px solid #FECACA',
            color: '#DC2626',
            fontSize: 12,
            fontWeight: 500,
          }}
        >
          {errorMsg}
        </div>
      )}

      {displayImageUrl && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            padding: 8,
            borderRadius: 8,
            background: '#F8FAFC',
            border: '1px solid #E2E8F0',
          }}
        >
          <button
            type="button"
            onClick={() => setLightboxUrl(displayImageUrl)}
            style={{
              padding: 0,
              border: '1px solid #CBD5E1',
              borderRadius: 6,
              overflow: 'hidden',
              cursor: 'pointer',
              background: '#0F172A',
              flexShrink: 0,
            }}
            title="Bấm để phóng to ảnh công tơ"
          >
            <img
              src={displayImageUrl}
              alt={`Công tơ ${meterLabel}`}
              style={{ width: 54, height: 42, objectFit: 'cover', display: 'block' }}
            />
          </button>
          <div style={{ flex: 1, minWidth: 0, fontSize: 12, color: '#334155' }}>
            {scanResult ? (
              <>
                <div style={{ fontWeight: 600, color: '#0F172A' }}>
                  {scanResult.rawDigits ? `Dãy số: ${scanResult.rawDigits}` : 'Đã phân tích ảnh'}
                </div>
                {scanResult.aiNotes && (
                  <div style={{ fontSize: 11, color: '#64748B', marginTop: 2 }}>{scanResult.aiNotes}</div>
                )}
              </>
            ) : (
              <div style={{ fontSize: 11, color: '#64748B' }}>Ảnh công tơ đã lưu (bấm để phóng to)</div>
            )}
          </div>
        </div>
      )}

      {scanResult?.warnings && scanResult.warnings.length > 0 && (
        <div
          style={{
            padding: '6px 10px',
            borderRadius: 6,
            background: scanResult.validationStatus === 'Error' ? '#FEF2F2' : '#FFFBEB',
            border: `1px solid ${scanResult.validationStatus === 'Error' ? '#FECACA' : '#FDE68A'}`,
            color: scanResult.validationStatus === 'Error' ? '#B91C1C' : '#92400E',
            fontSize: 11,
            display: 'flex',
            flexDirection: 'column',
            gap: 2,
          }}
        >
          {scanResult.warnings.map((w, idx) => (
            <div key={idx}>• {w}</div>
          ))}
        </div>
      )}

      {webcamModal}
      <ImageLightboxModal
        imageUrl={lightboxUrl}
        title={`Ảnh công tơ ${meterLabel}${label ? ` — ${label}` : ''}`}
        subtitle={
          scanResult?.readingValue !== undefined
            ? `Chỉ số AI đọc: ${scanResult.readingValue} (Độ tin cậy ${Math.round(scanResult.confidence * 100)}%)`
            : undefined
        }
        onClose={() => setLightboxUrl(null)}
      />
    </div>
  );
};
