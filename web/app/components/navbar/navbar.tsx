"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import Image from "next/image";

export default function Navbar() {
  const pathname = usePathname();

  const links = [
    { href: "/", label: "Início" },
    { href: "/sobre", label: "Sobre" },
    { href: "/dashboards", label: "Dashboards" },
    { href: "/denuncia", label: "Denúncia" },
  ];

  return (
    <nav className="w-full bg-neutral-900 text-white border-b border-gray-700">
      <div className="flex items-center justify-between py-5 px-[128px] max-w-[1920px] mx-auto">
        <Link href="/" className="flex items-center gap-2">
          <Image
            src="/title2.svg"
            alt="SafeZone Logo"
            width={75}
            height={75}
            className="object-contain"
          />
        </Link>

        <div className="flex items-center gap-8">
          {links.map((link) => {
            const active = pathname === link.href;
            return (
              <Link
                key={link.href}
                href={link.href}
                className={`text-sm md:text-base transition-all duration-200 pb-0 ${
                  active
                    ? "border-b-2 border-cyan-400 font-semibold"
                    : "text-gray-300 hover:text-white border-b-2 border-transparent"
                }`}
              >
                {link.label}
              </Link>
            );
          })}
        </div>
      </div>
    </nav>
  );
}
