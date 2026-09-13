import React from 'react';
interface HourglassLogoProps {
  className?: string;
}
const HourglassLogo: React.FC<HourglassLogoProps> = ({
  className = "w-8 h-8"
}) => {
  return <img src="https://d64gsuwffb70l.cloudfront.net/6879424911dc7495580c3269_1766293166295_e5526bf8.png" alt="Momentary Momentos Logo" className={className} />;
};
export default HourglassLogo;