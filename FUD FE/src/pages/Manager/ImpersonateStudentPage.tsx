import React, { useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import {
  lookupStudentApi,
  createImpersonationTokenApi,
  type StudentLookupResult,
} from '../../services/managerApi';

export const ImpersonateStudentPage: React.FC = () => {
  const { currentUser, startImpersonation } = useAuth();
  const [query, setQuery] = useState('');
  const [isSearching, setIsSearching] = useState(false);
  const [isStarting, setIsStarting] = useState(false);
  const [foundStudent, setFoundStudent] = useState<StudentLookupResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const handleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const cleanQuery = query.trim();
    if (!cleanQuery || cleanQuery.length < 2) {
      setErrorMsg('Vui lòng nhập tối thiểu 2 ký tự (Mã sinh viên hoặc Email).');
      return;
    }

    setIsSearching(true);
    setErrorMsg(null);
    setSuccessMsg(null);
    setFoundStudent(null);

    try {
      const student = await lookupStudentApi(cleanQuery);
      setFoundStudent(student);
    } catch (err: any) {
      setErrorMsg(err.message || 'Không tìm thấy sinh viên nào với thông tin đã nhập.');
    } finally {
      setIsSearching(false);
    }
  };

  const handleStartImpersonate = async () => {
    if (!foundStudent || !currentUser) return;

    if (foundStudent.accountStatus?.toLowerCase() !== 'active') {
      setErrorMsg(`Tài khoản sinh viên đang ở trạng thái ${foundStudent.accountStatus}, không thể đăng nhập hộ.`);
      return;
    }

    setIsStarting(true);
    setErrorMsg(null);

    try {
      const result = await createImpersonationTokenApi(foundStudent.id);
      startImpersonation({
        student: {
          id: result.student.id,
          code: result.student.code,
          fullName: result.student.fullName,
          email: result.student.email,
          gender: result.student.gender,
          primaryRole: 'Student',
          roles: result.student.roles || ['Student'],
          accountStatus: result.student.accountStatus,
          suspensionReason: result.student.suspensionReason,
          dormId: result.student.dormId,
          dormName: result.student.dormName,
        },
        token: result.impersonationToken,
        expiresAt: Date.now() + result.expiresIn * 1000,
        managerId: currentUser.id,
      });
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể bắt đầu phiên đăng nhập hộ. Vui lòng thử lại.');
      setIsStarting(false);
    }
  };

  return (
    <div style={{ padding: '28px 36px', maxWidth: '880px', margin: '0 auto' }}>
      {/* Tiêu đề trang */}
      <div style={{ marginBottom: '24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '6px' }}>
          <span style={{ fontSize: '28px' }}>🛡️</span>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: '#0F172A' }}>
            Đăng nhập thay Sinh viên
          </h1>
        </div>
        <p style={{ color: '#64748B', fontSize: '14px', margin: 0, lineHeight: 1.6 }}>
          Chức năng dành cho Ban Quản lý hỗ trợ sinh viên khi gặp khó khăn, khúc mắc trong quá trình đặt phòng,
          gửi đơn từ hoặc xem thông tin nội trú. Bạn có thể tra cứu và đăng nhập trực tiếp vào giao diện sinh viên.
        </p>
      </div>

      {/* Form tìm kiếm */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: '12px',
          padding: '24px',
          boxShadow: '0 1px 3px rgba(0, 0, 0, 0.08)',
          border: '1px solid #E2E8F0',
          marginBottom: '24px',
        }}
      >
        <form onSubmit={handleSearch}>
          <label
            htmlFor="studentSearchInput"
            style={{
              display: 'block',
              fontWeight: 600,
              fontSize: '14px',
              color: '#334155',
              marginBottom: '8px',
            }}
          >
            Nhập Email FPT hoặc Mã sinh viên (MSSV):
          </label>
          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
            <input
              id="studentSearchInput"
              type="text"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setErrorMsg(null);
              }}
              placeholder="Ví dụ: SE170001 hoặc sinhvien@fpt.edu.vn"
              style={{
                flex: 1,
                minWidth: '280px',
                padding: '12px 16px',
                borderRadius: '8px',
                border: '1.5px solid #CBD5E1',
                fontSize: '15px',
                outline: 'none',
                transition: 'border-color 0.15s',
              }}
              onFocus={(e) => (e.target.style.borderColor = '#EA580C')}
              onBlur={(e) => (e.target.style.borderColor = '#CBD5E1')}
            />
            <button
              type="submit"
              disabled={isSearching}
              style={{
                padding: '12px 24px',
                backgroundColor: '#EA580C',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '8px',
                fontSize: '15px',
                fontWeight: 600,
                cursor: isSearching ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                boxShadow: '0 2px 6px rgba(234, 88, 12, 0.3)',
              }}
            >
              {isSearching ? (
                <>
                  <span>⏳</span>
                  <span>Đang tìm...</span>
                </>
              ) : (
                <>
                  <span>🔍</span>
                  <span>Tìm kiếm sinh viên</span>
                </>
              )}
            </button>
          </div>
        </form>

        {errorMsg && (
          <div
            style={{
              marginTop: '16px',
              padding: '12px 16px',
              backgroundColor: '#FEF2F2',
              border: '1px solid #F87171',
              borderRadius: '8px',
              color: '#991B1B',
              fontSize: '14px',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
            }}
          >
            <span>⚠️</span>
            <span>{errorMsg}</span>
          </div>
        )}

        {successMsg && (
          <div
            style={{
              marginTop: '16px',
              padding: '12px 16px',
              backgroundColor: '#F0FDF4',
              border: '1px solid #86EFAC',
              borderRadius: '8px',
              color: '#166534',
              fontSize: '14px',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
            }}
          >
            <span>✅</span>
            <span>{successMsg}</span>
          </div>
        )}
      </div>

      {/* Kết quả thông tin sinh viên */}
      {foundStudent && (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1.5px solid #E2E8F0',
            boxShadow: '0 4px 12px rgba(0, 0, 0, 0.05)',
            overflow: 'hidden',
          }}
        >
          {/* Header Card */}
          <div
            style={{
              padding: '20px 24px',
              backgroundColor: '#FFF7ED',
              borderBottom: '1px solid #FED7AA',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '12px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
              <div
                style={{
                  width: '52px',
                  height: '52px',
                  borderRadius: '50%',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '22px',
                  fontWeight: 700,
                  boxShadow: '0 2px 6px rgba(234, 88, 12, 0.35)',
                }}
              >
                {foundStudent.fullName.trim().charAt(0).toUpperCase()}
              </div>
              <div>
                <h2 style={{ margin: '0 0 4px', fontSize: '18px', fontWeight: 700, color: '#0F172A' }}>
                  {foundStudent.fullName}
                </h2>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      backgroundColor: '#FFEDD5',
                      color: '#9A3412',
                      fontSize: '12px',
                      fontWeight: 700,
                    }}
                  >
                    MSSV: {foundStudent.code}
                  </span>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      backgroundColor:
                        foundStudent.accountStatus === 'Active'
                          ? '#DCFCE7'
                          : foundStudent.accountStatus === 'Suspended'
                          ? '#FEF3C7'
                          : '#FEE2E2',
                      color:
                        foundStudent.accountStatus === 'Active'
                          ? '#15803D'
                          : foundStudent.accountStatus === 'Suspended'
                          ? '#B45309'
                          : '#B91C1C',
                      fontSize: '12px',
                      fontWeight: 700,
                    }}
                  >
                    {foundStudent.accountStatus === 'Active'
                      ? '● Đang hoạt động'
                      : foundStudent.accountStatus === 'Suspended'
                      ? '● Đình chỉ nội trú'
                      : `● ${foundStudent.accountStatus}`}
                  </span>
                </div>
              </div>
            </div>

            <button
              type="button"
              onClick={handleStartImpersonate}
              disabled={isStarting}
              style={{
                padding: '12px 24px',
                backgroundColor: '#1D4ED8',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '8px',
                fontSize: '15px',
                fontWeight: 700,
                cursor: isStarting ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                boxShadow: '0 3px 10px rgba(29, 78, 216, 0.35)',
                transition: 'all 0.15s ease',
              }}
              onMouseEnter={(e) => {
                if (!isStarting) e.currentTarget.style.backgroundColor = '#1E40AF';
              }}
              onMouseLeave={(e) => {
                if (!isStarting) e.currentTarget.style.backgroundColor = '#1D4ED8';
              }}
            >
              {isStarting ? (
                <>
                  <span>⏳</span>
                  <span>Đang khởi tạo phiên...</span>
                </>
              ) : (
                <>
                  <span>🔑</span>
                  <span>Đăng nhập hộ sinh viên này</span>
                </>
              )}
            </button>
          </div>

          {/* Chi tiết thông tin */}
          <div style={{ padding: '24px' }}>
            <h3 style={{ fontSize: '15px', fontWeight: 700, color: '#334155', margin: '0 0 14px' }}>
              📋 Thông tin tài khoản & Hồ sơ sinh viên
            </h3>
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
                gap: '12px 24px',
                fontSize: '14px',
                color: '#1E293B',
                marginBottom: '20px',
                backgroundColor: '#F8FAFC',
                padding: '16px',
                borderRadius: '8px',
                border: '1px solid #E2E8F0',
              }}
            >
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Họ và tên:</span>
                <strong>{foundStudent.fullName}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Mã sinh viên (MSSV):</span>
                <strong>{foundStudent.code}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Email FPT:</span>
                <strong>{foundStudent.email}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Số điện thoại:</span>
                <strong>{foundStudent.phone || 'Chưa cập nhật'}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Giới tính:</span>
                <strong>{foundStudent.gender === 'M' ? 'Nam' : foundStudent.gender === 'F' ? 'Nữ' : foundStudent.gender || '—'}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Chuyên ngành:</span>
                <strong>{foundStudent.major || '—'}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Khóa tuyển sinh:</span>
                <strong>{foundStudent.cohort || '—'}</strong>
              </div>
              <div>
                <span style={{ color: '#64748B', display: 'block', fontSize: '12px' }}>Trạng thái tài khoản:</span>
                <span
                  style={{
                    color: foundStudent.accountStatus === 'Active' ? '#15803D' : '#B91C1C',
                    fontWeight: 600,
                  }}
                >
                  {foundStudent.accountStatus}
                </span>
              </div>
            </div>

            {/* Thông tin phòng ở hiện tại (nếu có) */}
            <h3 style={{ fontSize: '15px', fontWeight: 700, color: '#334155', margin: '20px 0 12px' }}>
              🏠 Trạng thái lưu trú Ký túc xá
            </h3>
            {foundStudent.currentResidency ? (
              <div
                style={{
                  backgroundColor: '#ECFDF5',
                  border: '1.5px solid #A7F3D0',
                  borderRadius: '8px',
                  padding: '16px',
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  gap: '12px 20px',
                  fontSize: '14px',
                }}
              >
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Khu KTX:</span>
                  <strong style={{ color: '#065F46' }}>{foundStudent.currentResidency.dormName || 'KTX FUD'}</strong>
                </div>
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Tòa / Block:</span>
                  <strong style={{ color: '#065F46' }}>{foundStudent.currentResidency.blockName || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Số phòng:</span>
                  <strong style={{ color: '#065F46' }}>Phòng {foundStudent.currentResidency.roomNumber || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Mã giường:</span>
                  <strong style={{ color: '#065F46' }}>Giường {foundStudent.currentResidency.bedCode || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Học kỳ:</span>
                  <strong style={{ color: '#065F46' }}>{foundStudent.currentResidency.termName || '—'}</strong>
                </div>
                <div>
                  <span style={{ color: '#047857', display: 'block', fontSize: '12px' }}>Tình trạng:</span>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      backgroundColor: '#10B981',
                      color: '#FFFFFF',
                      fontSize: '12px',
                      fontWeight: 700,
                    }}
                  >
                    {foundStudent.currentResidency.status}
                  </span>
                </div>
              </div>
            ) : (
              <div
                style={{
                  backgroundColor: '#F8FAFC',
                  border: '1px solid #E2E8F0',
                  borderRadius: '8px',
                  padding: '14px 16px',
                  color: '#64748B',
                  fontSize: '14px',
                }}
              >
                Sinh viên hiện chưa có hồ sơ lưu trú đang hoạt động trong kỳ này.
              </div>
            )}

            {/* Thông báo nhắc nhở quyền thao tác */}
            <div
              style={{
                marginTop: '20px',
                padding: '12px 16px',
                backgroundColor: '#EFF6FF',
                border: '1px solid #BFDBFE',
                borderRadius: '8px',
                fontSize: '13px',
                color: '#1E40AF',
                lineHeight: 1.5,
              }}
            >
              💡 <strong>Lưu ý nghiệp vụ:</strong> Khi bấm <em>"Đăng nhập hộ sinh viên này"</em>,
              hệ thống sẽ cấp token ủy quyền với đầy đủ quyền thao tác của sinh viên. Mọi hành động gửi đơn,
              đặt phòng sẽ được lưu lại với danh nghĩa của sinh viên và ghi nhật ký truy vết quản lý.
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
