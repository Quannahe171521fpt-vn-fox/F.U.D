import React, { useState } from 'react';
import type { TermManagementItem } from '../../../types/management';

interface DeleteTermModalProps {
  isOpen: boolean;
  term: TermManagementItem | null;
  onClose: () => void;
  onConfirm: (termId: number) => Promise<void>;
}

export const DeleteTermModal: React.FC<DeleteTermModalProps> = ({
  isOpen,
  term,
  onClose,
  onConfirm,
}) => {
  const [deleting, setDeleting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen || !term) return null;

  const handleDelete = async () => {
    try {
      setDeleting(true);
      setError(null);
      await onConfirm(term.id);
      onClose();
    } catch (err: any) {
      setError(err.message || 'Lỗi khi xóa học kỳ');
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="modal-backdrop-custom">
      <div className="term-modal-card delete-modal-card">
        <div className="modal-header-custom danger-header">
          <div className="modal-title-group">
            <span className="modal-icon-badge danger-badge">🗑️</span>
            <div>
              <h3 className="modal-main-title">Xác nhận xóa học kỳ</h3>
              <p className="modal-sub-title">Thao tác này sẽ xóa học kỳ khỏi cơ sở dữ liệu</p>
            </div>
          </div>
          <button type="button" className="modal-close-btn" onClick={onClose} disabled={deleting}>
            ✕
          </button>
        </div>

        <div className="modal-body-custom">
          {error && (
            <div className="term-error-banner">
              <span className="error-icon">⚠️</span>
              <span>{error}</span>
            </div>
          )}

          {term.hasDependentData ? (
            <div className="delete-blocked-warning">
              <div className="blocked-icon">🚫</div>
              <div className="blocked-info">
                <h4>Không thể xóa học kỳ này!</h4>
                <p>
                  Học kỳ <strong>{term.displayName}</strong> hiện đã có dữ liệu sinh viên phát sinh (Đơn đăng ký giữ phòng, đăng ký phòng mới, hợp đồng lưu trú hoặc hóa đơn tiền phòng/điện nước).
                </p>
                <p className="sub-blocked-note">
                  Để đảm bảo tính toàn vẹn dữ liệu, hệ thống không cho phép xóa các học kỳ đã có hoạt động nghiệp vụ.
                </p>
              </div>
            </div>
          ) : (
            <div className="delete-confirm-content">
              <p>
                Bạn có chắc chắn muốn xóa học kỳ <strong>{term.displayName}</strong> (Thời gian: {term.startDate} đến {term.endDate})?
              </p>
              <div className="delete-warning-note">
                ⚠️ Thao tác này sẽ xóa toàn bộ các cấu hình giai đoạn đăng ký (Keeping/Booking) và bảng giá nháp liên quan đến học kỳ này. Thao tác không thể hoàn tác.
              </div>
            </div>
          )}
        </div>

        <div className="modal-footer-custom">
          <button
            type="button"
            className="btn-secondary-custom"
            onClick={onClose}
            disabled={deleting}
          >
            Đóng
          </button>
          {!term.hasDependentData && (
            <button
              type="button"
              className="btn-danger-custom"
              onClick={handleDelete}
              disabled={deleting}
            >
              {deleting ? 'Đang xóa...' : 'Xác nhận xóa'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
