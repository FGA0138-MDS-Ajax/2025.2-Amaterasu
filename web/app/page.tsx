'use client'

import { IoIosArrowRoundDown } from "react-icons/io";

export default function Home() {
  return (
    <div className="min-h-screen bg-[#1F1F1F] flex flex-col items-center justify-center">
      <nav className="mb-8">
        <ul className="flex gap-8 text-white">
          <li>Sobre</li>
          <li>Dashboards</li>
          <li>Denúncia</li>
        </ul>
      </nav>
      
      <img 
        src="title.svg" 
        alt="Logo SafeZone" 
        className="mb-8" 
      />
      
      <div className="flex flex-col items-center">
        <p className="text-gray-400">Role para baixo</p>
        
        <p className="text-gray-400 text-2xl animate-bounce"><IoIosArrowRoundDown /></p>
      </div>
    </div>
  )
}