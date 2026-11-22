interface DecodedImage {
  width: number;
  height: number;
  draw: (ctx: CanvasRenderingContext2D, targetWidth: number, targetHeight: number) => void;
  cleanup: () => void;
}

async function decodeImage(file: File): Promise<DecodedImage> {
  if (typeof createImageBitmap === 'function') {
    const bitmap = await createImageBitmap(file);
    return {
      width: bitmap.width,
      height: bitmap.height,
      draw: (ctx, targetWidth, targetHeight) => {
        ctx.drawImage(bitmap, 0, 0, targetWidth, targetHeight);
      },
      cleanup: () => {
        bitmap.close();
      },
    };
  }

  const objectUrl = URL.createObjectURL(file);
  try {
    const img = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      element.onload = () => resolve(element);
      element.onerror = () => reject(new Error('Không thể giải mã ảnh'));
      element.src = objectUrl;
    });

    return {
      width: img.naturalWidth || img.width,
      height: img.naturalHeight || img.height,
      draw: (ctx, targetWidth, targetHeight) => {
        ctx.drawImage(img, 0, 0, targetWidth, targetHeight);
      },
      cleanup: () => {
        URL.revokeObjectURL(objectUrl);
      },
    };
  } catch (err) {
    URL.revokeObjectURL(objectUrl);
    throw err;
  }
}

export async function compressMeterImage(
  file: File,
  maxWidth = 1600,
  quality = 0.85
): Promise<File> {
  if (!file || file.size === 0) return file;

  let decoded: DecodedImage | null = null;
  try {
    decoded = await decodeImage(file);
    if (decoded.width <= 0 || decoded.height <= 0) {
      return file;
    }

    const scale = decoded.width > maxWidth ? maxWidth / decoded.width : 1;
    const targetWidth = Math.max(1, Math.round(decoded.width * scale));
    const targetHeight = Math.max(1, Math.round(decoded.height * scale));

    const canvas = document.createElement('canvas');
    canvas.width = targetWidth;
    canvas.height = targetHeight;

    const ctx = canvas.getContext('2d');
    if (!ctx) return file;

    decoded.draw(ctx, targetWidth, targetHeight);

    const blob = await new Promise<Blob | null>((resolve) => {
      canvas.toBlob((result) => resolve(result), 'image/jpeg', quality);
    });

    if (!blob || blob.size === 0 || (scale === 1 && blob.size >= file.size)) {
      return file;
    }

    const baseName = file.name.replace(/\.[^/.]+$/, '') || 'meter';
    return new File([blob], `${baseName}.jpg`, {
      type: 'image/jpeg',
      lastModified: Date.now(),
    });
  } catch {
    return file;
  } finally {
    decoded?.cleanup();
  }
}
