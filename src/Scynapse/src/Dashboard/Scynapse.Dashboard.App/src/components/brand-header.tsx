import React from 'react';
import logo from '../assets/img/ScynapseLogo.png';

export default function BrandHeader() {
  return (
    <div className="brand-link">
      <a href="#" style={{ textDecoration: 'none' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <img src={logo} alt="Scynapse Logo" className="brand-logo" />
          <h1 className="brand-title">
            Scynapse Dashboard
          </h1>
        </div>
      </a>
      <div id="version-content" className="brand-version"></div>
    </div>
  );
}
