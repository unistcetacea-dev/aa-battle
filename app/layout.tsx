import type { Metadata } from 'next';
import './globals.css';
export const metadata: Metadata = {
  title: 'AA BATTLE LAB · 창작 포켓몬 배틀',
  description: '아스키아트와 함께하는 창작 포켓몬 배틀 테스트 빌드',
};
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ko">
      <body>{children}</body>
    </html>
  );
}
